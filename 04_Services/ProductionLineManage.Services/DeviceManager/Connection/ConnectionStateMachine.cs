using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DeviceManager;

namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// 工位连接状态机：以 DeviceStatus.ConnectionState 为唯一数据源，驱动看板与重连计数。
    /// </summary>
    public sealed class ConnectionStateMachine
    {
        #region ===================== 字段 =====================

        /// <summary> 工位 Id </summary>
        private readonly int _stationId;

        /// <summary> 看板状态管理器 </summary>
        private readonly IDeviceStatusManager _deviceStatus;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 创建连接状态机 </summary>
        public ConnectionStateMachine(int stationId, IDeviceStatusManager deviceStatus)
        {
            _stationId = stationId;
            _deviceStatus = deviceStatus;
        }

        #endregion

        #region ===================== 状态查询 =====================

        /// <summary> 当前连接状态 </summary>
        public ConnectionState Current =>
            _deviceStatus.GetStatus(_stationId)?.ConnectionState ?? ConnectionState.Disconnected;

        /// <summary> 累计重连次数 </summary>
        public int ReconnectAttempts =>
            _deviceStatus.GetStatus(_stationId)?.ReconnectAttempts ?? 0;

        /// <summary> 是否处于已连接 </summary>
        public bool IsConnected => Current == ConnectionState.Connected;

        /// <summary> 是否正在连接或重连中 </summary>
        public bool IsConnectingOrReconnecting =>
            Current is ConnectionState.Connecting or ConnectionState.Reconnecting;

        #endregion

        #region ===================== 状态转换 =====================

        /// <summary> 切换为连接中 </summary>
        public void SetConnecting() => TransitionTo(ConnectionState.Connecting);

        /// <summary> 切换为已连接，重置重连计数并刷新心跳时间 </summary>
        public void SetConnected() =>
            TransitionTo(ConnectionState.Connected, status =>
            {
                status.ReconnectAttempts = 0;
                status.LastHeartbeat = DateTime.Now;
                status.LastError = string.Empty;
            });

        /// <summary> 切换为已断开 </summary>
        public void SetDisconnected(string? lastError = null) =>
            TransitionTo(ConnectionState.Disconnected, status =>
            {
                if (lastError != null)
                    status.LastError = lastError;
            });

        /// <summary> 切换为重连中，重连计数 +1 </summary>
        public void SetReconnecting() =>
            TransitionTo(ConnectionState.Reconnecting, status => status.ReconnectAttempts++);

        /// <summary> 切换为错误状态 </summary>
        public void SetError(string lastError) =>
            TransitionTo(ConnectionState.Error, status => status.LastError = lastError);

        /// <summary> 统一状态迁移入口 </summary>
        private void TransitionTo(ConnectionState state, Action<DeviceStatus>? update = null)
        {
            _deviceStatus.UpdateStatus(_stationId, status =>
            {
                status.ConnectionState = state;
                update?.Invoke(status);
            });
        }

        #endregion
    }
}
