// ProductionLineManage.Core/Enums/ConnectionState.cs
namespace ProductionLineManage.Core.Enums
{
    /// <summary>设备连接状态</summary>
    public enum ConnectionState
    {
        /// <summary>已断开</summary>
        Disconnected = 0,

        /// <summary>连接中</summary>
        Connecting = 1,

        /// <summary>已连接</summary>
        Connected = 2,

        /// <summary>重连中</summary>
        Reconnecting = 3,

        /// <summary>连接异常</summary>
        Error = 4
    }
}
