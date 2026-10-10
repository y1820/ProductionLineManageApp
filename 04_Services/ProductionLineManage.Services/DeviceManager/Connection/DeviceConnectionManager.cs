using Prism.Events;
using ProductionLineManage.Core.Abstractions;
using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.DeviceManager.InteractionType;
using ProductionLineManage.Infrastructure.Data.Repository;
using ProductionLineManage.Infrastructure.Logging;
using ProductionLineManage.Services.DeviceManager.Business.BusinessLogic;
using System.Collections.Concurrent;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 设备连接管理器：管理所有工位的 DeviceConnectionTask，支持单工位与全量启停。
    /// </summary>
    public class DeviceConnectionManager : IDeviceConnectionManager
    {
        #region ===================== 字段 =====================

        /// <summary> 工位连接任务字典（StationId → DeviceConnectionTask） </summary>
        private readonly ConcurrentDictionary<int, DeviceConnectionTask> _connections = new();

        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        /// <summary> Prism 事件聚合器 </summary>
        private readonly IEventAggregator _eventAggregator;

        /// <summary> 数据缓存（设备配置、地址映射等） </summary>
        private readonly IDataCacheService _cacheService;

        /// <summary> 集成式共享驱动池 </summary>
        private readonly ISharedDriverPool _sharedDriverPool;

        /// <summary> 业务中介（Interaction 注册与消息分发） </summary>
        private readonly IDeviceBusinessMediator _businessMediator;

        /// <summary> 设备看板状态 </summary>
        private readonly IDeviceStatusManager _statusManager;

        /// <summary> 交互类型工厂 </summary>
        private readonly IInteractionTypeFactory _interactionFactory;

        private readonly IRequestCodes _requestCodes;
        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建设备连接管理器 </summary>
        public DeviceConnectionManager(
            ILogger logger,
            IEventAggregator eventAggregator,
            IDataCacheService cacheService,
            ISharedDriverPool sharedDriverPool,
            IDeviceBusinessMediator businessMediator,
            IDeviceStatusManager deviceStatus,
            IInteractionTypeFactory interactionFactory,
            IRequestCodes requestCodes)
        {
            _logger = logger;
            _eventAggregator = eventAggregator;
            _cacheService = cacheService;
            _sharedDriverPool = sharedDriverPool;
            _businessMediator = businessMediator;
            _statusManager = deviceStatus;
            _interactionFactory = interactionFactory;
            _requestCodes = requestCodes;
        }

        #endregion

        #region ===================== IDeviceConnectionManager =====================

        /// <summary> 启动单个工位连接：创建 Task 并注册 Interaction </summary>
        public async Task<bool> StartStationAsync(device_ConnectInfo config)
        {
            if (_connections.ContainsKey(config.StationId)) // 避免重复开启
            {
                _logger.DeviceLog(config.StationId, config.DeviceCode, $"工位连接已存在: StationId={config.StationId}");
                return true;
            }

            var task = new DeviceConnectionTask(
                config, _logger, _cacheService, _eventAggregator,
                _sharedDriverPool, _businessMediator, _statusManager, _interactionFactory, _requestCodes);

            if (_connections.TryAdd(config.StationId, task))
            {
                _logger.DeviceLog(config.StationId, config.DeviceCode, $"启动工位连接: StationId={config.StationId}");
                return await task.StartAsync();
            }

            return false;
        }

        /// <summary> 启动所有已启用工位连接 </summary>
        public async Task StartAllAsync()
        {
            _logger.Info("开始启动所有设备连接...");

            try
            {
                var allConfigs = _cacheService.GetData<List<device_ConnectInfo>>();
                if (allConfigs == null || !allConfigs.Any())
                {
                    _logger.Warning("没有找到设备连接配置");
                    return;
                }

                var enabledConfigs = allConfigs.Where(c => c.IsEnabled).ToList(); // 仅启用的配置
                _logger.Info($"找到 {enabledConfigs.Count} 个启用的设备配置");

                foreach (var config in enabledConfigs)
                {
                    await StartStationAsync(config);
                }

                _logger.Info($"设备连接启动完成，成功启动 {_connections.Count} 个连接");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "启动所有设备连接失败");
            }
        }

        /// <summary> 停止单个工位连接并释放资源 </summary>
        public async Task StopStationAsync(int stationId)
        {
            if (_connections.TryRemove(stationId, out var task))
            {
                _logger.DeviceLog(stationId, "管理器", $"停止工位连接: StationId={stationId}");
                await task.StopAsync();
                task.Dispose();
            }
            else
            {
                _logger.DeviceLog(stationId, "管理器", $"工位连接不存在: StationId={stationId}",
                    LogLevel.Warning);
            }
        }

        /// <summary> 停止所有工位连接 </summary>
        public async Task StopAllAsync()
        {
            _logger.Info("开始停止所有设备连接...");

            var tasks = _connections.Values.ToList();
            _connections.Clear(); // 先清空字典，避免 Stop 期间重复操作

            foreach (var task in tasks)
            {
                await task.StopAsync();
                task.Dispose();
            }

            _logger.Info($"所有设备连接已停止，共停止 {tasks.Count} 个连接");
        }

        #endregion

        #region ===================== IDisposable =====================

        /// <summary> 同步停止所有连接（最多等待 5 秒） </summary>
        public void Dispose()
        {
            StopAllAsync().Wait(5000);
        }

        #endregion
    }
}
