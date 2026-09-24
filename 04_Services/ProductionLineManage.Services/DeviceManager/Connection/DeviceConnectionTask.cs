using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.DeviceManager.InteractionType;
using ProductionLineManage.Infrastructure.Logging;
using ProductionLineManage.Services.DeviceManager.Drivers;
using Prism.Events;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 单个工位的连接任务编排器。
    /// 协调连接、采集、型号下发与业务 Channel，自身尽量保持薄。
    /// </summary>
    public class DeviceConnectionTask : IDisposable, IDeviceTaskContext
    {
        #region ===================== 字段 =====================

        #region --------------------- 只读依赖 ---------------------

        private readonly device_ConnectInfo _config;
        private readonly ILogger _logger;
        private readonly IDataCacheService _cacheService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ISharedDriverPool _sharedDriverPool;
        private readonly IDeviceBusinessMediator _businessMediator;
        private readonly IInteractionTypeFactory _interactionFactory;
        private readonly IDeviceStatusManager _deviceStatus;

        #endregion

        #region --------------------- 子模块 ---------------------

        private readonly StationBusinessChannel _businessChannel;
        private readonly StationConnection _connection;
        private readonly StationAcquisition _acquisition;
        private readonly StationIssueModelService _issueModel;
        private readonly StationTaskIo _taskIo;

        #endregion

        #region --------------------- 线程与令牌 ---------------------

        private CancellationTokenSource? _cts;
        private Task? _workTask;
        private Task? _connectionTask;
        private Task? _businessTask;

        #endregion

        #endregion

        #region ===================== 基础信息属性 =====================

        /// <summary> 工位 Id </summary>
        public int StationId => _config.StationId;

        /// <summary> 工位工艺信息（从缓存加载） </summary>
        public craft_StationInfo StationInfo { get; } = new craft_StationInfo();

        /// <summary> 交互类型（指令型 / 信号型等） </summary>
        public string InteractionType => _config.InteractionType;

        /// <summary> 是否由软件下发型号 </summary>
        public bool IsIssueModel => _config.IsIssueModel;

        /// <summary> 是否配置了读取型号地址映射 </summary>
        public bool HasProductTypeReadMapping => _acquisition.HasProductTypeReadMapping;

        /// <inheritdoc />
        public bool HasMapping(string dataName) => _acquisition.HasMapping(dataName);

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建工位连接任务（默认本地 SharedDriverPool） </summary>
        public DeviceConnectionTask(
            device_ConnectInfo config,
            ILogger logger,
            IDataCacheService cacheService,
            IEventAggregator eventAggregator,
            IDeviceBusinessMediator businessMediator,
            IDeviceStatusManager deviceStatus,
            IInteractionTypeFactory interactionFactory)
            : this(config, logger, cacheService, eventAggregator, null, businessMediator, deviceStatus, interactionFactory)
        {
        }

        /// <summary> 创建工位连接任务并组装子模块（连接 / 采集 / 型号 / IO / 业务队列） </summary>
        public DeviceConnectionTask(
            device_ConnectInfo config,
            ILogger logger,
            IDataCacheService cacheService,
            IEventAggregator eventAggregator,
            ISharedDriverPool? sharedDriverPool,
            IDeviceBusinessMediator businessMediator,
            IDeviceStatusManager deviceStatus,
            IInteractionTypeFactory interactionFactory)
        {
            _config = config;
            _logger = logger;
            _cacheService = cacheService;
            _eventAggregator = eventAggregator;
            _sharedDriverPool = sharedDriverPool ?? new SharedDriverPool(logger);
            _deviceStatus = deviceStatus;
            _businessMediator = businessMediator;
            _interactionFactory = interactionFactory;

            _businessChannel = new StationBusinessChannel(
                businessMediator, this, logger, config.InteractionType, config.ScanIntervalMs);

            // bootstrap：Connection 心跳回调依赖 Acquisition，IssueModel / TaskIo 依赖二者
            StationAcquisition? acquisitionBootstrap = null;

            _connection = new StationConnection(
                config,
                logger,
                deviceStatus,
                _sharedDriverPool,
                onConnected: token => acquisitionBootstrap!.PrepareAfterConnectionAsync(
                    _connection.GetDriver(), _config.ProtocolType, token),
                onDisconnected: () =>
                {
                    acquisitionBootstrap!.ResetValueCache();
                    _businessChannel.ResetCodeGate();
                },
                hasHeartbeatMapping: () => acquisitionBootstrap!.HasMapping(DataNameConstants.Heartbeat),
                getHeartbeatMapping: () =>
                {
                    acquisitionBootstrap!.TryGetMapping(DataNameConstants.Heartbeat, out var mapping);
                    return mapping;
                });

            acquisitionBootstrap = new StationAcquisition(
                config,
                logger,
                cacheService,
                deviceStatus,
                _businessChannel,
                () => _connection.GetDriver(),
                (reason, token) => _connection.MarkDisconnectedAsync(reason, token));

            _acquisition = acquisitionBootstrap;

            _issueModel = new StationIssueModelService(
                config, logger, cacheService, _acquisition, () => _connection.GetDriver());

            _taskIo = new StationTaskIo(_acquisition, _connection, CanUseDriverForIo);

            _deviceStatus.UpdateStatus(StationId, status =>
            {
                status.StationId = StationId;
                status.DeviceCode = config.DeviceCode;
                status.ConnectionState = ConnectionState.Disconnected;
                status.RunState = RunState.Stopped;
                status.UpdateTime = DateTime.Now;
            });
            //初始化工位信息
            if (_cacheService.HasData<List<craft_StationInfo>>())//查看缓存数据是否存在工位信息
            {
                var stations = _cacheService.GetData<List<craft_StationInfo>>();//获取所有工位
                var station = stations.Find(s => s.Id == StationId);//提取Id相同的工位信息
                if (station != null)
                    StationInfo = station;//供外部接口使用
            }

            _eventAggregator.GetEvent<IssueModelEvent>().Subscribe(
                type => _ = _issueModel.IssueAsync(type),
                ThreadOption.BackgroundThread);
        }

        #endregion

        #region ===================== 启停 =====================

        /// <summary> 启动：加载映射 → 注册 Interaction → 启动三线程 </summary>
        public async Task<bool> StartAsync()
        {
            if (_cts != null)
                return false;

            await _acquisition.LoadMappingsAsync();

            try
            {
                var interaction = _interactionFactory.Create(_config.InteractionType, this);
                await _businessMediator.RegisterStationAsync(StationId, interaction);
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"交互类型已注册: LogicType={interaction.LogicType}");
            }
            catch (Exception ex)
            {
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"交互类型注册失败: {ex.Message}", LogLevel.Error);
                return false;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _businessTask = Task.Run(() => _businessChannel.RunConsumerAsync(token), token);
            _workTask = Task.Run(WorkerLoopAsync);
            _connectionTask = Task.Run(() => _connection.RunLoopAsync(token));

            _logger.DeviceLog(StationId, _config.DeviceCode,
                $"设备连接任务启动: 工位={_config.StationId}, 设备={_config.DeviceCode}, 逻辑类型={_config.InteractionType}");
            return true;
        }

        /// <summary> 停止：Cancel → 等待线程 → 释放连接 → 注销 Interaction </summary>
        public async Task StopAsync()
        {
            if (_cts == null)
                return;

            _logger.DeviceLog(StationId, _config.DeviceCode,
                $"设备连接任务停止: 工位={_config.StationId}, 设备={_config.DeviceCode}");

            _cts.Cancel();

            await WaitTaskOrObserveAsync(_workTask, "工作任务停止超时（扫描周期未完成）", LogLevel.Error);
            _businessChannel.Complete();
            await WaitTaskOrObserveAsync(_businessTask, "业务队列停止超时", LogLevel.Warning);
            await WaitTaskOrObserveAsync(_connectionTask, "连接任务停止超时", LogLevel.Error);

            _connection.ReleaseOnStop();
            await _businessMediator.UnregisterStationAsync(StationId);

            _cts.Dispose();
            _cts = null;
            _workTask = null;
            _connectionTask = null;
            _businessTask = null;
        }

        /// <summary> 等待 Task 完成，超时则后台观察 </summary>
        private async Task WaitTaskOrObserveAsync(Task? task, string timeoutMessage, LogLevel level)
        {
            if (task == null)
                return;

            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"{timeoutMessage}: 工位={_config.StationId}", level);
                BackgroundTaskObserver.Observe(task);
            }
        }

        #endregion

        #region ===================== 工作线程 =====================

        /// <summary> 采集线程：扫描 + 首次型号处理 </summary>
        private async Task WorkerLoopAsync()
        {
            if (_cts == null)
            {
                _logger.DeviceLog(StationId, _config.DeviceCode, "工作线程异常，任务令牌对象为空", LogLevel.Error);
                return;
            }

            var token = _cts.Token;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_connection.IsConnected)
                    {
                        var driver = _connection.GetDriver();
                        if (driver == null || !driver.IsConnected)
                            await _connection.MarkDisconnectedAsync("连接已断开", token);
                        else
                            await _acquisition.RunScanCycleAsync(token);

                        if (token.IsCancellationRequested)
                            break;

                        await _issueModel.TryIssueOnceOnWorkerLoopAsync();
                    }

                    if (token.IsCancellationRequested)
                        break;

                    await Task.Delay(_config.ScanIntervalMs, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.DeviceLog(StationId, _config.DeviceCode,
                        $"设备工作线程异常: 工位={_config.StationId}, 设备={_config.DeviceCode} " + ex.Message, LogLevel.Error);
                    await Task.Delay(5000, token);
                }
            }

            _logger.DeviceLog(StationId, _config.DeviceCode,
                $"收到停止令牌，设备工作线程结束: 工位={_config.StationId}, 设备={_config.DeviceCode}", LogLevel.Warning);
        }

        #endregion

        #region ===================== IDeviceTaskContext（委托 TaskIo） =====================

        /// <inheritdoc />
        public Task<object?> ReadAsync(string dataName) => _taskIo.ReadByDataNameAsync(dataName);

        /// <inheritdoc />
        public Task<object?> ReadAsync(string address, string dataType, int dataLength) =>
            _taskIo.ReadByAddressAsync(address, dataType, dataLength);

        /// <inheritdoc />
        public Task<bool> WriteAsync(string dataName, object value) =>
            _taskIo.WriteByDataNameAsync(dataName, value);

        /// <inheritdoc />
        public Task<bool> WriteAsync(string address, string dataType, object value, int dataLength) =>
            _taskIo.WriteByAddressAsync(address, dataType, value, dataLength);

        /// <inheritdoc />
        public void Log(string message, LogLevel level = LogLevel.Info) =>
            _logger.DeviceLog(_config.StationId, _config.DeviceCode, message, level);

        /// <summary> Stop 期间仅扫描周期内允许 Interaction 回写 PLC </summary>
        private bool CanUseDriverForIo()
        {
            if (_connection.GetDriver() == null || _cts == null)
                return false;
            if (!_cts.Token.IsCancellationRequested)
                return true;
            return _acquisition.IsScanCycleInProgress;
        }

        #endregion

        #region ===================== IDisposable =====================

        /// <summary> 同步取消并等待线程结束，分布式模式释放本地驱动 </summary>
        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();

            _workTask?.Wait(3000); // 等待采集线程
            _connectionTask?.Wait(3000); // 等待连接线程

            if (_config.ConnectionMode != ConnectTypeConstants.Centralized)
                _connection.ReleaseOnStop(); // 分布式需主动释放驱动
        }

        #endregion
    }

    /// <summary> 按协议类型创建设备驱动实例 </summary>
    public static class DeviceDriverFactory
    {
        /// <summary> 根据协议字符串创建对应 IDeviceCommunication 实现 </summary>
        public static IDeviceCommunication Create(string protocolType, ILogger? logger = null)
        {
            return protocolType switch
            {
                DeviceProtocolTypeConstants.Simulator => new SimulatorConnection(),
                DeviceProtocolTypeConstants.S7 => new S7Connection(logger),
                DeviceProtocolTypeConstants.OPCUA => new OpcUaConnection(),
                _ => new SimulatorConnection() // 未知协议回退模拟器
            };
        }
    }
}
