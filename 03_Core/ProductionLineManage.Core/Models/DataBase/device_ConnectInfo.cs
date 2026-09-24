using ProductionLineManage.Core.Constants;

namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 设备通信配置（每个工位独立配置） </summary>
    public class device_ConnectInfo : BaseEntity
    {
        #region ===================== 工位与设备标识 =====================

        /// <summary> 工位 ID </summary>
        public int StationId { get; set; }

        /// <summary> 设备编码 </summary>
        public string DeviceCode { get; set; } = string.Empty;

        #endregion

        #region ===================== 协议与连接 =====================

        /// <summary> 协议类型: S7, Modbus, OPCUA, Hsl, Simulator </summary>
        public string ProtocolType { get; set; } = DeviceProtocolTypeConstants.Simulator;

        /// <summary> 连接模式: Distributed(独立连接), Centralized(中央PLC共享连接) </summary>
        public string ConnectionMode { get; set; } = ConnectTypeConstants.Distributed;

        /// <summary> 连接字符串 (IP:Port) </summary>
        public string ConnectionString { get; set; } = string.Empty;

        #endregion

        #region ===================== 扫描与心跳 =====================

        /// <summary> 扫描周期（毫秒）- 读取设备数据的间隔 </summary>
        public int ScanIntervalMs { get; set; } = 500;

        /// <summary> 心跳间隔（毫秒） </summary>
        public int HeartbeatIntervalMs { get; set; } = 3000;

        /// <summary> 心跳超时时间（毫秒） </summary>
        public int HeartbeatTimeoutMs { get; set; } = 10000;

        /// <summary> 重连间隔（毫秒） </summary>
        public int ReconnectIntervalMs { get; set; } = 5000;

        /// <summary> 最大重连次数（-1 无限） </summary>
        public int MaxReconnectAttempts { get; set; } = -1;

        #endregion

        #region ===================== 交互与开关 =====================

        /// <summary> 逻辑类型，决定设备的交互逻辑方式是指令型:100、200或单一信号型 </summary>
        public string InteractionType { get; set; } = InteractionTypeConstants.Command;

        /// <summary> 是否由软件下发型号 </summary>
        public bool IsIssueModel { get; set; }

        /// <summary> 是否启用 </summary>
        public bool IsEnabled { get; set; } = true;

        #endregion
    }
}
