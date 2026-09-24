using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 分布式连接策略：每工位独立创建驱动并 Connect，断线时释放本地驱动。
    /// </summary>
    public sealed class DistributedConnectionStrategy : IConnectionModeStrategy
    {
        #region ===================== 字段 =====================

        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建分布式连接策略 </summary>
        public DistributedConnectionStrategy(ILogger logger)
        {
            _logger = logger;
        }

        #endregion

        #region ===================== IConnectionModeStrategy =====================

        /// <summary> 创建驱动并尝试 Connect </summary>
        public async Task<ConnectionAcquireResult> AcquireAsync(device_ConnectInfo config, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return ConnectionAcquireResult.Cancelled();

            try
            {
                var driver = DeviceDriverFactory.Create(config.ProtocolType, _logger);
                var success = await driver.ConnectAsync(config);
                if (token.IsCancellationRequested)
                {
                    driver.Dispose();
                    return ConnectionAcquireResult.Cancelled();
                }

                if (success && driver.IsConnected)
                    return ConnectionAcquireResult.Succeeded(driver);

                driver.Dispose();
                _logger.DeviceLog(config.StationId, config.DeviceCode, "分布式连接失败");
                return ConnectionAcquireResult.Failed();
            }
            catch (OperationCanceledException)
            {
                return ConnectionAcquireResult.Cancelled();
            }
            catch (Exception ex)
            {
                _logger.DeviceLog(config.StationId, config.DeviceCode,
                    $"分布式连接失败: {ex.Message}", LogLevel.Error);
                return ConnectionAcquireResult.Errored(ex);
            }
        }

        /// <summary> Stop 时断开并释放本地驱动 </summary>
        public void ReleaseOnStop(device_ConnectInfo config, IDeviceCommunication? driver)
        {
            driver?.Disconnect();
            driver?.Dispose();
        }

        /// <summary> 本地断线标记时断开并释放驱动 </summary>
        public void ReleaseOnLocalDisconnect(IDeviceCommunication? driver)
        {
            driver?.Disconnect();
            driver?.Dispose();
        }

        #endregion
    }
}
