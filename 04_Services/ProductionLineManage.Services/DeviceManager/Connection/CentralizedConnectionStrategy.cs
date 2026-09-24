using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 集中式（集成）连接策略：从 SharedDriverPool 获取共享 PlcSession 驱动。
    /// </summary>
    public sealed class CentralizedConnectionStrategy : IConnectionModeStrategy
    {
        #region ===================== 字段 =====================

        /// <summary> 共享驱动池 </summary>
        private readonly ISharedDriverPool _sharedDriverPool;

        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建集中式连接策略 </summary>
        public CentralizedConnectionStrategy(ISharedDriverPool sharedDriverPool, ILogger logger)
        {
            _sharedDriverPool = sharedDriverPool;
            _logger = logger;
        }

        #endregion

        #region ===================== IConnectionModeStrategy =====================

        /// <summary> 从共享池获取或等待 PlcSession 连接 </summary>
        public async Task<ConnectionAcquireResult> AcquireAsync(device_ConnectInfo config, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return ConnectionAcquireResult.Cancelled();

            try
            {
                var driver = await _sharedDriverPool.GetOrCreateAsync(config);
                if (token.IsCancellationRequested)
                    return ConnectionAcquireResult.Cancelled();

                if (driver.IsConnected)
                    return ConnectionAcquireResult.Succeeded(driver);

                _logger.DeviceLog(config.StationId, config.DeviceCode, "共享连接不可用或重连失败");
                return ConnectionAcquireResult.Failed();
            }
            catch (OperationCanceledException)
            {
                return ConnectionAcquireResult.Cancelled();
            }
            catch (Exception ex)
            {
                _logger.DeviceLog(config.StationId, config.DeviceCode,
                    $"集中式连接失败: {ex.Message}", LogLevel.Error);
                return ConnectionAcquireResult.Errored(ex);
            }
        }

        /// <summary> Stop 时 Detach 工位引用（引用归零才关闭 Session） </summary>
        public void ReleaseOnStop(device_ConnectInfo config, IDeviceCommunication? driver)
        {
            _sharedDriverPool.Release(config);
        }

        /// <summary> 本地断线：仅标记工位断开，不断开共享驱动 </summary>
        public void ReleaseOnLocalDisconnect(IDeviceCommunication? driver)
        {
            // 集中式：PlcSession 由维护循环统一管理物理连接
        }

        #endregion
    }
}
