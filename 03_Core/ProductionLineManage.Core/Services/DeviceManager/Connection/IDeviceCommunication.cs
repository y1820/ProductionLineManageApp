using ProductionLineManage.Core.Models.DataBase;
using System;
using System.Threading.Tasks;

namespace ProductionLineManage.Core.Services.DeviceManager.Connection
{
    /// <summary> 设备通信接口：所有协议驱动（S7、OPC UA 等）必须实现 </summary>
    public interface IDeviceCommunication : IDisposable
    {
        #region ===================== 连接状态 =====================

        /// <summary> 是否已连接 </summary>
        bool IsConnected { get; }

        /// <summary> 连接设备 </summary>
        Task<bool> ConnectAsync(device_ConnectInfo config);

        /// <summary> 断开连接 </summary>
        void Disconnect();

        #endregion

        #region ===================== 读写操作 =====================

        /// <summary> 读取 PLC 数据 </summary>
        Task<object?> ReadAsync(string dataAddress, string dataType, int dataLen);

        /// <summary> 写入 PLC 数据 </summary>
        Task<bool> WriteAsync(string dataAddress, string dataType, object value, int dataLen);

        #endregion

        #region ===================== 心跳与订阅 =====================

        /// <summary> 发送心跳 </summary>
        Task<bool> HeartbeatAsync(string dataType, string address);

        /// <summary> OPC UA 订阅所有地址数据 </summary>
        Task SubscribeAllAsync(List<device_AddressMapping> mappings, Action<string, object?> valueHandler);

        #endregion
    }
}
