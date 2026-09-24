namespace ProductionLineManage.Core.Constants
{
    /// <summary> 通讯协议类型常量 </summary>
    public static class DeviceProtocolTypeConstants
    {
        /// <summary> 西门子 S7 协议 </summary>
        public const string S7 = "S7";

        /// <summary> Modbus 协议 </summary>
        public const string Modbus = "Modbus";

        /// <summary> OPC UA 协议 </summary>
        public const string OPCUA = "OPCUA";

        /// <summary> TCP/IP 协议 </summary>
        public const string TCPIP = "TCPIP";

        /// <summary> 模拟器（本地 C# 类型） </summary>
        public const string Simulator = "Simulator";

        /// <summary> 协议类型选项（UI 下拉） </summary>
        public static IReadOnlyList<string> ProtocolTypes { get; } = new[]
        {
            S7,
            OPCUA,
            Simulator
        };
    }
}
