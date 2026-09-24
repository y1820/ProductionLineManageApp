using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 单个 PLC Key 的物理连接会话：唯一负责 Open/重连，工位仅 Attach/Detach。
    /// </summary>
    internal sealed class PlcSession
    {
        #region ===================== 常量 =====================

        /// <summary> 同步 Open 最长阻塞（TCP 超时约 21s，留余量） </summary>
        private const int ConnectAttemptWaitMs = 25_000;

        #endregion

        #region ===================== 字段 =====================

        /// <summary> PLC 唯一 Key（协议 + 连接串） </summary>
        private readonly string _key;

        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        /// <summary> 保护 Session 内部状态 </summary>
        private readonly object _sync = new();

        /// <summary> 串行化 Connect 尝试 </summary>
        private readonly SemaphoreSlim _connectGate = new(1, 1);

        /// <summary> 线程安全包装后的驱动 </summary>
        private SharedDriverPool.ThreadSafeDriver? _driver;

        /// <summary> 首个 Attach 工位的配置模板 </summary>
        private device_ConnectInfo? _configTemplate;

        /// <summary> 已 Attach 的工位 Id 集合 </summary>
        private readonly HashSet<int> _stationIds = new();

        /// <summary> 引用计数 </summary>
        private int _refCount;

        /// <summary> 维护循环取消令牌 </summary>
        private CancellationTokenSource? _maintainCts;

        /// <summary> 维护循环 Task </summary>
        private Task? _maintainTask;

        /// <summary> 当前进行中的 Connect Task </summary>
        private Task? _inFlightConnectTask;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建 PlcSession </summary>
        public PlcSession(string key, ILogger logger)
        {
            _key = key;
            _logger = logger;
        }

        #endregion

        #region ===================== 对外属性 =====================

        /// <summary> 物理连接是否就绪 </summary>
        public bool IsConnected
        {
            get
            {
                lock (_sync)
                {
                    return _driver?.IsConnected == true;
                }
            }
        }

        /// <summary> 是否仍有工位引用 </summary>
        public bool HasReferences => GetRefCount() > 0;

        #endregion

        #region ===================== Attach / Detach =====================

        /// <summary> 工位挂载并等待首次连接（启动时用） </summary>
        public Task<IDeviceCommunication?> AttachAsync(device_ConnectInfo config, CancellationToken token) =>
            WaitForConnectionAsync(config, token);

        /// <summary> 等待 Session 连接就绪，与维护循环 TryConnectOnce 节奏对齐 </summary>
        public async Task<IDeviceCommunication?> WaitForConnectionAsync(
            device_ConnectInfo config,
            CancellationToken token)
        {
            RegisterStation(config);
            EnsureMaintainLoop();

            if (IsConnected)
                return _driver;

            var interval = Math.Max(500, config.ReconnectIntervalMs);

            while (!token.IsCancellationRequested)
            {
                if (IsConnected)
                    return _driver;

                var attempt = GetInFlightConnectTask();
                if (attempt != null)
                {
                    try
                    {
                        await attempt.WaitAsync(TimeSpan.FromMilliseconds(ConnectAttemptWaitMs), token);
                    }
                    catch (TimeoutException)
                    {
                        // 单次 Open 超时，继续等维护循环下一轮
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }

                    if (IsConnected)
                        return _driver; // attempt 结束后立即复查

                    continue;
                }

                // 无进行中的 Open：分段轮询等待
                var waitedMs = 0;
                const int pollSliceMs = 200;
                while (waitedMs < interval && !token.IsCancellationRequested)
                {
                    if (IsConnected)
                        return _driver;

                    if (GetInFlightConnectTask() != null)
                        break;

                    var slice = Math.Min(pollSliceMs, interval - waitedMs);
                    try
                    {
                        await Task.Delay(slice, token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }

                    waitedMs += slice;
                }
            }

            return IsConnected ? _driver : null;
        }

        /// <summary> 工位卸载；引用归零时停止维护循环并释放驱动 </summary>
        public void Detach(device_ConnectInfo config)
        {
            lock (_sync)
            {
                if (!_stationIds.Remove(config.StationId))
                    return;

                _refCount--;
                _logger.DeviceLog(config.StationId, config.DeviceCode,
                    $"PlcSession Detach: Key={_key}, 工位={config.StationId}, 剩余引用={_refCount}");

                if (_refCount > 0)
                    return;
            }

            StopMaintainLoop();
            DisposeDriver();
            _logger.DeviceLog(config.StationId, config.DeviceCode,
                $"PlcSession 关闭: Key={_key}");
        }

        #endregion

        #region ===================== 工位注册 =====================

        /// <summary> 注册工位并增加引用计数 </summary>
        private void RegisterStation(device_ConnectInfo config)
        {
            lock (_sync)
            {
                _configTemplate ??= config;
                if (_stationIds.Add(config.StationId))
                {
                    _refCount++;
                    _logger.DeviceLog(config.StationId, config.DeviceCode,
                        $"PlcSession Attach: Key={_key}, 工位={config.StationId}, 引用={_refCount}");
                }
            }
        }

        /// <summary> 获取当前进行中的 Connect Task </summary>
        private Task? GetInFlightConnectTask()
        {
            lock (_sync)
            {
                return _inFlightConnectTask;
            }
        }

        /// <summary> 获取引用计数 </summary>
        private int GetRefCount()
        {
            lock (_sync)
            {
                return _refCount;
            }
        }

        #endregion

        #region ===================== 维护循环 =====================

        /// <summary> 确保后台维护循环已启动 </summary>
        private void EnsureMaintainLoop()
        {
            lock (_sync)
            {
                if (_maintainTask != null && !_maintainTask.IsCompleted)
                    return;

                _maintainCts = new CancellationTokenSource();
                _maintainTask = Task.Run(() => MaintainLoopAsync(_maintainCts.Token));
            }
        }

        /// <summary> 停止维护循环 </summary>
        private void StopMaintainLoop()
        {
            CancellationTokenSource? cts;
            lock (_sync)
            {
                cts = _maintainCts;
                _maintainCts = null;
                _maintainTask = null;
                _inFlightConnectTask = null;
            }

            cts?.Cancel();
            cts?.Dispose();
        }

        /// <summary> 维护循环：断线时周期性 TryConnectOnce </summary>
        private async Task MaintainLoopAsync(CancellationToken token)
        {
            _logger.Info($"PlcSession 维护循环启动: Key={_key}", nameof(PlcSession));

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (GetRefCount() == 0)
                        break;

                    if (!IsConnected)
                        await RunConnectAttemptAsync(token);

                    if (token.IsCancellationRequested || GetRefCount() == 0)
                        break;

                    var interval = _configTemplate?.ReconnectIntervalMs ?? 3000;
                    await Task.Delay(Math.Max(500, interval), token);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常停止
            }
            catch (Exception ex)
            {
                _logger.Error($"PlcSession 维护循环异常: Key={_key}, {ex.Message}", nameof(PlcSession));
            }
            finally
            {
                _logger.Info($"PlcSession 维护循环结束: Key={_key}", nameof(PlcSession));
            }
        }

        /// <summary> 包装单次 Connect 尝试并跟踪 in-flight Task </summary>
        private async Task RunConnectAttemptAsync(CancellationToken token)
        {
            var attemptTask = TryConnectOnceAsync(token);
            lock (_sync)
            {
                _inFlightConnectTask = attemptTask;
            }

            try
            {
                await attemptTask;
            }
            finally
            {
                lock (_sync)
                {
                    if (_inFlightConnectTask == attemptTask)
                        _inFlightConnectTask = null;
                }
            }
        }

        /// <summary> 执行一次物理 Connect </summary>
        private async Task TryConnectOnceAsync(CancellationToken token)
        {
            device_ConnectInfo? config;
            lock (_sync)
            {
                config = _configTemplate;
            }

            if (config == null || token.IsCancellationRequested)
                return;

            await _connectGate.WaitAsync(token);
            try
            {
                if (IsConnected)
                    return;

                SharedDriverPool.ThreadSafeDriver driver;
                lock (_sync)
                {
                    if (_driver == null)
                    {
                        var inner = DeviceDriverFactory.Create(config.ProtocolType, _logger);
                        _driver = new SharedDriverPool.ThreadSafeDriver(inner);
                    }
                    driver = _driver;
                }

                _logger.DeviceLog(config.StationId, config.DeviceCode,
                    $"PlcSession 物理连接: Key={_key}, 工位={config.StationId}");

                var connected = await driver.ConnectAsync(config);
                if (!connected)
                {
                    _logger.DeviceLog(config.StationId, config.DeviceCode,
                        $"PlcSession 连接失败: Key={_key}", LogLevel.Warning);
                }
            }
            finally
            {
                _connectGate.Release();
            }
        }

        #endregion

        #region ===================== 释放 =====================

        /// <summary> 断开并释放驱动，清空工位引用 </summary>
        private void DisposeDriver()
        {
            lock (_sync)
            {
                _driver?.Disconnect();
                _driver?.Dispose();
                _driver = null;
                _configTemplate = null;
                _stationIds.Clear();
            }
        }

        #endregion
    }
}
