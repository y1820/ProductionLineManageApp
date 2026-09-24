using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;
using ProductionLineManage.Services.DeviceManager.Business;
using ProductionLineManage.Services.DeviceManager.Connection;
using Prism.Events;

namespace ProductionLineManage.Services.DeviceManager
{
    /// <summary>
    /// 设备管理服务门面实现：协调连接、业务、状态等子模块，对外提供统一接口，本身不包含具体业务逻辑。
    /// </summary>
    public class DeviceManagementService : IDeviceManagementService
    {
        #region ===================== 私有字段 =====================

        private readonly IDeviceConnectionManager _connectionManager;
        private readonly IDeviceBusinessMediator _businessMediator;
        private readonly IDeviceStatusManager _statusManager;
        private readonly IDataCacheService _cacheService;
        private readonly ILogger _logger;
        private readonly IEventAggregator _eventAggregator;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入依赖并订阅状态变更转发 </summary>
        public DeviceManagementService(
            IDataCacheService dataCacheService,
            ILogger logger,
            IDeviceConnectionManager connectionManager,
            IDeviceBusinessMediator businessMediator,
            IDeviceStatusManager statusManager,
            IEventAggregator eventAggregator)
        {
            _logger = logger;
            _cacheService = dataCacheService;
            _connectionManager = connectionManager;
            _businessMediator = businessMediator;
            _statusManager = statusManager;
            _eventAggregator = eventAggregator;

            _statusManager.StatusChanged += (s, stationId) =>
            {
                DeviceStatusChanged?.Invoke(this, stationId); // 转发状态变更给 UI
            };
        }

        #endregion

        #region ===================== 状态订阅 =====================

        /// <summary> 工位状态变更事件 </summary>
        public event EventHandler<int>? DeviceStatusChanged;

        #endregion

        #region ===================== 设备启动控制 =====================

        /// <summary> 启动指定工位设备 </summary>
        public async Task<bool> StartDeviceAsync(int stationId)
        {
            try
            {
                _logger.Info($"门面：开始启动设备 {stationId}");

                var config = await GetDeviceConfigAsync(stationId); // 1. 获取设备配置
                if (config == null)
                {
                    _logger.Warning($"门面：未找到设备 {stationId} 的配置");
                    return false;
                }

                // 2. 启动连接（Interaction 注册在 DeviceConnectionTask.StartAsync 内完成）
                var connected = await _connectionManager.StartStationAsync(config);
                if (!connected)
                {
                    _logger.Warning($"门面：设备 {stationId} 连接失败");
                    return false;
                }

                _logger.Info($"门面：设备 {stationId} 启动成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"门面：启动设备 {stationId} 异常: {ex.Message}");
                return false;
            }
        }

        /// <summary> 停止指定工位设备 </summary>
        public async Task<bool> StopDeviceAsync(int stationId)
        {
            try
            {
                _logger.Info($"门面：开始停止设备 {stationId}");

                await _connectionManager.StopStationAsync(stationId); // Interaction 注销在 StopAsync 内完成

                _logger.Info($"门面：设备 {stationId} 停止成功");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"门面：停止设备 {stationId} 异常: {ex.Message}");
                return false;
            }
        }

        /// <summary> 启动所有已启用工位 </summary>
        public async Task<bool> StartAllDevicesAsync()
        {
            try
            {
                _logger.Info("门面：开始启动所有设备");

                // 下发第一个型号，没有型号则不下发
                if (_cacheService.HasData<List<craft_TypeInfo>>())
                {
                    var types = _cacheService.GetData<List<craft_TypeInfo>>();
                    if (types.Count > 0)
                    {
                        _eventAggregator.GetEvent<IssueModelEvent>().Publish(types[0]); // 发布第一个型号
                        _cacheService.SetData<craft_TypeInfo>(types[0]);//存入缓存，让未收到事件通知的对象在缓存里找发布的型号
                    }
                }

                var configs = _cacheService.GetData<List<device_ConnectInfo>>();//从缓存里找所有工位连接配置
                if (configs == null || !configs.Any())
                {
                    _logger.Warning("门面：未找到任何设备配置");
                    return false;
                }

                var enabledConfigs = configs.Where(c => c.IsEnabled).ToList();//筛选已启用的
                //筛选缓存里存在的工位
                var stations = _cacheService.GetData<List<craft_StationInfo>>();
                if (stations.Count == 0)//当缓存里工艺模块下的工位配置为0时直接退出
                {
                    _logger.Warning("门面：未在缓存里找到任何工艺模块下的工位配置，已退出设备连接");
                    return false;
                }
                var stationIdSet = stations.Select(s => s.Id).ToHashSet();
                enabledConfigs.RemoveAll(sc => !stationIdSet.Contains(sc.StationId));

                var successCount = 0;
                foreach (var config in enabledConfigs)
                {
                    if (await StartDeviceAsync(config.StationId))
                    {
                        successCount++;
                    }
                }

                _logger.Info($"门面：所有设备启动完成，成功 {successCount}/{enabledConfigs.Count()}");
                return successCount > 0;
            }
            catch (Exception ex)
            {
                _logger.Error($"门面：启动所有设备异常: {ex}");
                return false;
            }
        }

        /// <summary> 停止所有工位 </summary>
        public async Task<bool> StopAllDevicesAsync()
        {
            try
            {
                _logger.Info("门面：开始停止所有设备");

                var configs = _cacheService.GetData<List<device_ConnectInfo>>();
                if (configs == null) return true;

                foreach (var config in configs)
                {
                    await StopDeviceAsync(config.StationId);
                }

                _logger.Info("门面：所有设备已停止");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"门面：停止所有设备异常: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region ===================== 状态查询 =====================

        /// <summary> 获取单个工位状态 </summary>
        public DeviceStatus? GetDeviceStatus(int stationId)
        {
            return _statusManager.GetStatus(stationId);
        }

        /// <summary> 获取所有工位状态 </summary>
        public IReadOnlyDictionary<int, DeviceStatus> GetAllDeviceStatus()
        {
            return _statusManager.GetAllStatus();
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 从缓存获取工位连接配置 </summary>
        private async Task<device_ConnectInfo?> GetDeviceConfigAsync(int stationId)
        {
            var configs = _cacheService.GetData<List<device_ConnectInfo>>();
            await Task.CompletedTask;
            return configs?.FirstOrDefault(c => c.StationId == stationId);
        }

        #endregion
    }
}
