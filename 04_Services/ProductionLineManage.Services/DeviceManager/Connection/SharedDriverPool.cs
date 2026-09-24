using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;
using System.Collections.Concurrent;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 集成式连接池：每个 PLC Key 对应一个 PlcSession，工位仅 Attach/Detach。
    /// </summary>
    public class SharedDriverPool : ISharedDriverPool
    {
        #region ===================== 字段 =====================

        /// <summary> Key → PlcSession 映射 </summary>
        private readonly ConcurrentDictionary<string, PlcSession> _sessions = new();

        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建共享驱动池 </summary>
        public SharedDriverPool(ILogger logger)
        {
            _logger = logger;
        }

        #endregion

        #region ===================== ISharedDriverPool =====================

        /// <summary> 获取或等待共享驱动（连接失败则抛异常） </summary>
        public async Task<IDeviceCommunication> GetOrCreateAsync(device_ConnectInfo config)
        {
            var driver = await WaitForConnectionAsync(config, CancellationToken.None);
            if (driver == null || !driver.IsConnected)
                throw new InvalidOperationException($"共享驱动连接失败: Key={BuildKey(config)}");

            return driver;
        }

        /// <summary> 等待 PlcSession 连接就绪（工位连接线程轮询调用） </summary>
        public async Task<IDeviceCommunication?> WaitForConnectionAsync(
            device_ConnectInfo config,
            CancellationToken token)
        {
            var key = BuildKey(config);
            var session = _sessions.GetOrAdd(key, k => new PlcSession(k, _logger)); // 懒创建 Session
            return await session.WaitForConnectionAsync(config, token);
        }

        /// <summary> 工位 Detach；引用归零时移除 Session </summary>
        public void Release(device_ConnectInfo config)
        {
            var key = BuildKey(config);
            if (!_sessions.TryGetValue(key, out var session))
                return;

            session.Detach(config);
            if (!session.HasReferences)
                _sessions.TryRemove(key, out _); // 无引用时回收 Session
        }

        #endregion

        #region ===================== 内部工具 =====================

        /// <summary> 构建 PLC 唯一 Key：协议 + 连接串 </summary>
        private static string BuildKey(device_ConnectInfo config) =>
            $"{config.ProtocolType}|{config.ConnectionString}";

        #endregion

        #region ===================== ThreadSafeDriver =====================

        /// <summary> 线程安全的驱动包装器：Semaphore 串行化读写 </summary>
        public class ThreadSafeDriver : IDeviceCommunication
        {
            #region --------------------- 字段 ---------------------

            /// <summary> 被包装的真实驱动 </summary>
            private readonly IDeviceCommunication _inner;

            /// <summary> 串行化 IO 操作的信号量 </summary>
            private readonly SemaphoreSlim _semaphore = new(1, 1);

            /// <summary> Disconnect 专用锁 </summary>
            private readonly object _lockObj = new();

            /// <summary> 是否已释放 </summary>
            private bool _disposed;

            #endregion

            #region --------------------- 构造与属性 ---------------------

            /// <summary> 包装内部驱动 </summary>
            public ThreadSafeDriver(IDeviceCommunication inner)
            {
                _inner = inner;
            }

            /// <summary> 内部驱动引用（OPC 订阅等需类型判断时使用） </summary>
            public IDeviceCommunication Inner => _inner;

            /// <summary> 是否仍处于连接状态 </summary>
            public bool IsConnected => !_disposed && _inner.IsConnected;

            #endregion

            #region --------------------- 连接管理 ---------------------

            /// <summary> 线程安全 Connect </summary>
            public async Task<bool> ConnectAsync(device_ConnectInfo config)
            {
                if (_disposed) throw new ObjectDisposedException(GetType().FullName);
                if (IsConnected)
                    return true;

                await _semaphore.WaitAsync();
                try
                {
                    if (IsConnected)
                        return true;
                    return await _inner.ConnectAsync(config);
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            /// <summary> 断开连接 </summary>
            public void Disconnect()
            {
                if (_disposed) return;
                lock (_lockObj)
                {
                    _inner.Disconnect();
                }
            }

            /// <summary> 释放资源 </summary>
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Disconnect();
                _semaphore.Dispose();
            }

            #endregion

            #region --------------------- IO 操作 ---------------------

            /// <summary> 线程安全心跳 </summary>
            public async Task<bool> HeartbeatAsync(string dataType, string address)
            {
                if (_disposed) throw new ObjectDisposedException(GetType().FullName);
                await _semaphore.WaitAsync();
                try
                {
                    return await _inner.HeartbeatAsync(dataType, address);
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            /// <summary> 线程安全读取 </summary>
            public async Task<object?> ReadAsync(string dataAddress, string dataType, int dataLen)
            {
                if (_disposed) throw new ObjectDisposedException(GetType().FullName);
                await _semaphore.WaitAsync();
                try
                {
                    return await _inner.ReadAsync(dataAddress, dataType, dataLen);
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            /// <summary> 线程安全写入 </summary>
            public async Task<bool> WriteAsync(string dataAddress, string dataType, object value, int dataLen)
            {
                if (_disposed) throw new ObjectDisposedException(GetType().FullName);
                await _semaphore.WaitAsync();
                try
                {
                    return await _inner.WriteAsync(dataAddress, dataType, value, dataLen);
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            /// <summary> 线程安全 OPC 订阅 </summary>
            public async Task SubscribeAllAsync(
                List<device_AddressMapping> readMappings,
                Action<string, object?> valueHandler)
            {
                if (_disposed) throw new ObjectDisposedException(GetType().FullName);
                await _semaphore.WaitAsync();
                try
                {
                    await _inner.SubscribeAllAsync(readMappings, valueHandler);
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            #endregion
        }

        #endregion
    }
}
