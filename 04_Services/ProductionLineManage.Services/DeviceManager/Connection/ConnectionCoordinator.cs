using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 连接协调器：统一连接状态更新，委托策略处理集中式/分布式差异。
    /// </summary>
    public sealed class ConnectionCoordinator
    {
        #region ===================== 字段 =====================

        /// <summary> 工位连接配置 </summary>
        private readonly device_ConnectInfo _config;

        /// <summary> 连接模式策略（集中式 / 分布式） </summary>
        private readonly IConnectionModeStrategy _strategy;

        /// <summary> 连接状态机 </summary>
        private readonly ConnectionStateMachine _connectionState;

        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建连接协调器 </summary>
        public ConnectionCoordinator(
            device_ConnectInfo config,
            IConnectionModeStrategy strategy,
            ConnectionStateMachine connectionState,
            ILogger logger)
        {
            _config = config;
            _strategy = strategy;
            _connectionState = connectionState;
            _logger = logger;
        }

        #endregion

        #region ===================== 对外属性 =====================

        /// <summary> 工位 Id </summary>
        public int StationId => _config.StationId;

        #endregion

        #region ===================== 连接管理 =====================

        /// <summary> 尝试建立连接并更新状态机 </summary>
        public async Task<ConnectionAcquireResult> TryConnectAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return ConnectionAcquireResult.Cancelled();

            _connectionState.SetConnecting();

            var result = await _strategy.AcquireAsync(_config, token);
            switch (result.Status)
            {
                case ConnectionAttemptStatus.Success:
                    _connectionState.SetConnected();
                    _logger.DeviceLog(_config.StationId, _config.DeviceCode,
                        $"连接成功: 工位={_config.StationId}, 模式={_config.ConnectionMode}");
                    break;

                case ConnectionAttemptStatus.Failed:
                    _connectionState.SetDisconnected();
                    break;

                case ConnectionAttemptStatus.Error:
                    _connectionState.SetError(result.Error?.Message ?? "连接失败");
                    break;
            }

            return result;
        }

        /// <summary> Stop 时释放驱动资源 </summary>
        public void ReleaseOnStop(IDeviceCommunication? driver) =>
            _strategy.ReleaseOnStop(_config, driver);

        /// <summary> 本地断线时释放驱动（集中式通常为空操作） </summary>
        public void ReleaseOnLocalDisconnect(IDeviceCommunication? driver) =>
            _strategy.ReleaseOnLocalDisconnect(driver);

        #endregion
    }
}
