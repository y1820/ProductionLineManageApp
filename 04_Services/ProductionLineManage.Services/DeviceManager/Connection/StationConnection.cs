using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;
using ProductionLineManage.Services.DeviceManager.Drivers;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 工位连接生命周期：连接线程、分布式/集成式建连、心跳、重连与断线标记。
    /// 持有当前 <see cref="IDeviceCommunication"/> 驱动引用，供采集与 Context 读写使用。
    /// </summary>
    internal sealed class StationConnection
    {
        #region ===================== 字段 =====================

        #region --------------------- 只读依赖 ---------------------

        /// <summary> 工位连接配置（协议、连接串、重连间隔等） </summary>
        private readonly device_ConnectInfo _config;

        /// <summary> 设备日志 </summary>
        private readonly ILogger _logger;

        /// <summary> 看板状态（心跳时间、连接状态推送） </summary>
        private readonly IDeviceStatusManager _deviceStatus;

        /// <summary> 共享驱动池（集成式 PlcSession 模式） </summary>
        private readonly ISharedDriverPool _sharedDriverPool;

        /// <summary> 连接协调器：TryConnect、Release </summary>
        private readonly ConnectionCoordinator _connectionCoordinator;

        /// <summary> 连接状态机：Disconnected / Connecting / Connected / Reconnecting / Error </summary>
        private readonly ConnectionStateMachine _connectionStateMachine;

        #endregion

        #region --------------------- 回调（与 Task / Acquisition 协作） ---------------------

        /// <summary> 建连成功：重置采集缓存、初始化 OPC 订阅等 </summary>
        private readonly Func<CancellationToken, Task> _onConnected;

        /// <summary> 断线后：清空采集侧上一轮值缓存 </summary>
        private readonly Action _onDisconnected;

        /// <summary> 是否配置了心跳地址映射 </summary>
        private readonly Func<bool> _hasHeartbeatMapping;

        /// <summary> 取心跳地址映射（未配置时返回 null） </summary>
        private readonly Func<device_AddressMapping?> _getHeartbeatMapping;

        #endregion

        #region --------------------- 可变状态 ---------------------

        /// <summary> 保护 _driver 读写的锁 </summary>
        private readonly object _lockObj = new();

        /// <summary> 当前通信驱动（分布式为本工位独占，集成式为 PlcSession 包装） </summary>
        private IDeviceCommunication? _driver;

        /// <summary> MarkDisconnected 防抖门闩，避免工作/连接线程重复标记 </summary>
        private int _markDisconnectedGate;

        #endregion

        #endregion

        #region ===================== 对外属性 =====================

        /// <summary> 工位 Id </summary>
        public int StationId => _config.StationId;

        /// <summary> 状态机是否处于已连接 </summary>
        public bool IsConnected => _connectionStateMachine.IsConnected;

        /// <summary> 当前连接状态枚举 </summary>
        public ConnectionState CurrentState => _connectionStateMachine.Current;

        #endregion

        #region ===================== 构造 =====================

        /// <summary>
        /// 创建工位连接管理器。
        /// </summary>
        /// <param name="onConnected">物理连接就绪后的回调（订阅、缓存重置）</param>
        /// <param name="onDisconnected">标记断线后的回调（通常 ResetValueCache）</param>
        public StationConnection(
            device_ConnectInfo config,
            ILogger logger,
            IDeviceStatusManager deviceStatus,
            ISharedDriverPool sharedDriverPool,
            Func<CancellationToken, Task> onConnected,
            Action onDisconnected,
            Func<bool> hasHeartbeatMapping,
            Func<device_AddressMapping?> getHeartbeatMapping)
        {
            _config = config;
            _logger = logger;
            _deviceStatus = deviceStatus;
            _sharedDriverPool = sharedDriverPool;
            _onConnected = onConnected;
            _onDisconnected = onDisconnected;
            _hasHeartbeatMapping = hasHeartbeatMapping;
            _getHeartbeatMapping = getHeartbeatMapping;

            // 状态机与协调器：与 DeviceConnectionTask 原逻辑一致，集中在此维护连接语义
            _connectionStateMachine = new ConnectionStateMachine(config.StationId, deviceStatus);
            _connectionCoordinator = new ConnectionCoordinator(
                config,
                ConnectionModeStrategyFactory.Create(config, sharedDriverPool, logger),
                _connectionStateMachine,
                logger);
        }

        #endregion

        #region ===================== 驱动访问 =====================

        /// <summary> 线程安全地获取当前驱动（供采集、Context 读写、型号下发） </summary>
        public IDeviceCommunication? GetDriver()
        {
            lock (_lockObj)
            {
                return _driver; // 可能为 null（未连接或已释放）
            }
        }

        #endregion

        #region ===================== 连接线程入口 =====================

        /// <summary>
        /// 连接线程主循环：周期性 EnsureConnected + Delay。
        /// 由 DeviceConnectionTask 在 _connectionTask 中 Task.Run 调用。
        /// </summary>
        public async Task RunLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await EnsureConnectedAsync(token); // 维持连接或触发重连
                    if (token.IsCancellationRequested)
                        break;

                    await Task.Delay(GetLoopDelayMs(), token); // 已连接时按心跳间隔，断线时按重连间隔
                }
            }
            catch (OperationCanceledException)
            {
                // StopAsync Cancel 时正常退出
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested)
                    return;

                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"连接线程异常: {ex}", LogLevel.Error);
                try
                {
                    await Task.Delay(5000, token); // 异常后短暂退避，避免空转
                }
                catch (OperationCanceledException)
                {
                    // 停止时忽略
                }
            }
        }

        #endregion

        #region ===================== 连接管理 =====================

        /// <summary> 确保连接正常：已连接则心跳/断线检测；未连接则建连或等 PlcSession </summary>
        private async Task EnsureConnectedAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            if (_connectionStateMachine.IsConnected)
            {
                // 已连接：校验驱动存活 + 可选心跳
                if (_driver == null || !_driver.IsConnected)
                {
                    await MarkDisconnectedAsync("连接已断开", token);
                }
                else if (_hasHeartbeatMapping())
                {
                    var lastHb = _deviceStatus.GetStatus(StationId)?.LastHeartbeat;
                    if (DateTime.Now - lastHb > TimeSpan.FromMilliseconds(_config.HeartbeatTimeoutMs))
                    {
                        await MarkDisconnectedAsync("心跳超时", token); // 心跳长时间未更新
                    }
                    else
                    {
                        await HeartbeatAsync(token); // 本周期执行心跳读写
                        return;
                    }
                }
                else
                {
                    return; // 无心跳映射，保持连接即可
                }

                // 集成式：断线标记后同轮进入 Wait，不再 Delay(ReconnectInterval) 才等待
                if (_config.ConnectionMode != ConnectTypeConstants.Centralized)
                    return;
            }

            // 集成式：只等待 PlcSession 维护循环重连，工位不各自 Connect/ScheduleReconnect
            if (_config.ConnectionMode == ConnectTypeConstants.Centralized)
            {
                if (_connectionStateMachine.Current == ConnectionState.Error &&
                    _config.MaxReconnectAttempts > 0 &&
                    _connectionStateMachine.ReconnectAttempts >= _config.MaxReconnectAttempts)
                {
                    return; // 已达最大重连次数，不再尝试
                }

                if (!_connectionStateMachine.IsConnected)
                    await WaitForCentralizedSessionAsync(token);

                return;
            }

            // 分布式：需要连接或重连
            if (!_connectionStateMachine.IsConnectingOrReconnecting)
            {
                if (_connectionStateMachine.Current == ConnectionState.Error &&
                    _config.MaxReconnectAttempts > 0 &&
                    _connectionStateMachine.ReconnectAttempts >= _config.MaxReconnectAttempts)
                {
                    return;
                }

                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"工位正在连接: 工位={StationId}, 设备={_config.DeviceCode}");
                await ConnectAsync(token);
            }
        }

        /// <summary> 集成式：等待 PlcSession 物理连接，不在工位侧触发 Open </summary>
        private async Task WaitForCentralizedSessionAsync(CancellationToken token)
        {
            if (_connectionStateMachine.Current == ConnectionState.Disconnected)
            {
                // 已有共享驱动引用说明曾经连上过，显示重连中；否则为首次连接
                if (_driver != null)
                    _connectionStateMachine.SetReconnecting();
                else
                    _connectionStateMachine.SetConnecting();

                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"等待 PlcSession 重连: 工位={StationId}, Key={_config.ProtocolType}|{_config.ConnectionString}");
            }

            var driver = await _sharedDriverPool.WaitForConnectionAsync(_config, token);
            if (token.IsCancellationRequested)
                return;

            if (driver == null || !driver.IsConnected)
                return; // Session 尚未就绪

            lock (_lockObj)
            {
                _driver = driver; // 绑定共享驱动
            }

            _connectionStateMachine.SetConnected();
            await NotifyConnectedAsync(token); // 回调：ResetValueCache + OPC 订阅
            _logger.DeviceLog(StationId, _config.DeviceCode,
                $"PlcSession 重连成功，工位恢复在线: 工位={StationId}");
        }

        /// <summary> 连接线程每次循环的 Delay 毫秒数 </summary>
        private int GetLoopDelayMs()
        {
            if (_connectionStateMachine.IsConnected)
            {
                if (_hasHeartbeatMapping())
                    return Math.Max(100, _config.HeartbeatIntervalMs); // 有心跳则按心跳间隔
                return 1000;
            }

            if (_connectionStateMachine.Current is ConnectionState.Error or ConnectionState.Disconnected)
                return Math.Max(500, _config.ReconnectIntervalMs); // 断线/错误：较长间隔

            return 200; // Connecting / Reconnecting：短间隔轮询
        }

        /// <summary> 分布式模式：通过 ConnectionCoordinator 建立连接 </summary>
        private async Task ConnectAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            try
            {
                var result = await _connectionCoordinator.TryConnectAsync(token);
                if (token.IsCancellationRequested || result.Status == ConnectionAttemptStatus.Cancelled)
                    return;

                if (result.IsSuccess && result.Driver != null)
                {
                    lock (_lockObj)
                    {
                        _driver = result.Driver;
                    }
                    await NotifyConnectedAsync(token);
                    return;
                }

                lock (_lockObj)
                {
                    _driver = null;
                }

                await ScheduleReconnectAsync(token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested)
                    return;

                _connectionStateMachine.SetError(ex.Message);
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"设备连接失败: {ex.Message}", LogLevel.Error);
                await ScheduleReconnectAsync(token);
            }
        }

        /// <summary> 连接失败后进入重连：Delay + 再次 Connect </summary>
        private async Task ScheduleReconnectAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return;

            _connectionStateMachine.SetReconnecting();
            if (_config.MaxReconnectAttempts > 0 &&
                _connectionStateMachine.ReconnectAttempts >= _config.MaxReconnectAttempts)
            {
                _connectionStateMachine.SetError("已达到最大重连次数");
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"设备达到最大重连次数: 工位={StationId}, 设备={_config.DeviceCode}", LogLevel.Error);
                return;
            }

            _logger.DeviceLog(StationId, _config.DeviceCode,
                $"设备将重连: 工位={StationId}, 设备={_config.DeviceCode}, 第{_connectionStateMachine.ReconnectAttempts}次");
            try
            {
                await Task.Delay(_config.ReconnectIntervalMs, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested)
                return;

            await ConnectAsync(token);
        }

        /// <summary> 建连 / Session 恢复成功：通知 Task 侧重置采集并初始化 OPC </summary>
        private async Task NotifyConnectedAsync(CancellationToken token)
        {
            await _onConnected(token);
        }

        #endregion

        #region ===================== 断线标记 =====================

        /// <summary>
        /// 标记为断开连接（public：Acquisition.onReadFault、WorkerLoop 断线检查均会调用）。
        /// 含防抖 gate，避免多线程重复 Release。
        /// </summary>
        public async Task MarkDisconnectedAsync(string reason, CancellationToken token)
        {
            if (_connectionStateMachine.Current == ConnectionState.Disconnected)
                return;

            if (Interlocked.CompareExchange(ref _markDisconnectedGate, 1, 0) != 0)
                return; // 已有线程在处理断线

            try
            {
                if (token.IsCancellationRequested)
                {
                    _connectionStateMachine.SetDisconnected("设备离线");
                    return;
                }

                try
                {
                    await Task.Delay(5, token); // 短暂合并并发断线信号
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (_connectionStateMachine.Current == ConnectionState.Disconnected)
                    return;

                _connectionStateMachine.SetDisconnected(reason);
                _onDisconnected(); // 通常 ResetValueCache
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"设备连接断开: 工位={StationId}, 设备={_config.DeviceCode}, 原因={reason}", LogLevel.Error);

                lock (_lockObj)
                {
                    _connectionCoordinator.ReleaseOnLocalDisconnect(_driver);
                    if (_config.ConnectionMode != ConnectTypeConstants.Centralized)
                        _driver = null; // 分布式释放本地驱动引用；集成式保留 Session 引用
                }
            }
            finally
            {
                Interlocked.Exchange(ref _markDisconnectedGate, 0);
            }
        }

        #endregion

        #region ===================== 心跳 =====================

        /// <summary> 向 PLC 心跳地址写入/读取，更新 LastHeartbeat；失败则 MarkDisconnected </summary>
        private async Task HeartbeatAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested || _driver == null)
                return;

            var mapping = _getHeartbeatMapping();
            if (mapping == null)
            {
                _deviceStatus.UpdateStatus(StationId, status => { status.LastHeartbeat = DateTime.Now; });
                return; // 无映射时仅刷新时间戳
            }

            try
            {
                var success = await _driver.HeartbeatAsync(mapping.DataType, mapping.DataAddress);
                if (success)
                    _deviceStatus.UpdateStatus(StationId, status => { status.LastHeartbeat = DateTime.Now; });
                else if (!token.IsCancellationRequested)
                    await MarkDisconnectedAsync("心跳失败", token);
            }
            catch (Exception ex) when (ConnectionIoExceptionHelper.IsIoTimeout(ex))
            {
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"心跳写入超时（连接可能已断开）: {ex.Message}", LogLevel.Warning);
                await MarkDisconnectedAsync("心跳超时", token);
            }
            catch (Exception ex) when (ConnectionIoExceptionHelper.IsTransport(ex))
            {
                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"心跳传输异常: {ex.Message}", LogLevel.Warning);
                await MarkDisconnectedAsync("心跳失败", token);
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested)
                    return;

                _logger.DeviceLog(StationId, _config.DeviceCode,
                    $"心跳失败: {ex}", LogLevel.Error);
                await MarkDisconnectedAsync("心跳失败", token);
            }
        }

        #endregion

        #region ===================== 停止 / 释放 =====================

        /// <summary> StopAsync / Dispose 时释放驱动与状态机 </summary>
        public void ReleaseOnStop()
        {
            lock (_lockObj)
            {
                if (_config.ConnectionMode == ConnectTypeConstants.Centralized)
                    _logger.DeviceLog(StationId, _config.DeviceCode, "集成式断开连接");

                _connectionCoordinator.ReleaseOnStop(_driver);
                _driver = null;
                _connectionStateMachine.SetDisconnected("设备离线");
            }
        }

        #endregion
    }
}
