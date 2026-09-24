using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Core.Services.DeviceManager.Connection
{
    /// <summary> 设备任务上下文接口：供业务逻辑层读写 PLC 与记日志 </summary>
    public interface IDeviceTaskContext
    {
        #region ===================== 工位信息 =====================

        /// <summary> 工位 Id </summary>
        int StationId { get; }

        /// <summary> 交互类型（指令型/信号型） </summary>
        string InteractionType { get; }

        /// <summary> 工位工艺信息 </summary>
        craft_StationInfo StationInfo { get; }

        /// <summary> 是否由 SCADA 软件下发型号（否则由 PLC 决定） </summary>
        bool IsIssueModel { get; }

        /// <summary> 是否已配置「读取型号」地址映射（PLC 决定型号时使用） </summary>
        bool HasProductTypeReadMapping { get; }

        /// <summary> 是否已配置指定 DataName 的地址映射 </summary>
        bool HasMapping(string dataName);

        #endregion

        #region ===================== PLC 读写 =====================

        /// <summary> 按地址映射 DataName 读取（device_AddressMapping） </summary>
        Task<object?> ReadAsync(string dataName);

        /// <summary> 按原始 PLC 地址读取（craft_DataCollectConfig 加工数据采集） </summary>
        Task<object?> ReadAsync(string address, string dataType, int dataLength);

        /// <summary> 按地址映射 DataName 写入 </summary>
        Task<bool> WriteAsync(string dataName, object value);

        /// <summary> 按原始 PLC 地址写入（工位传值等场景） </summary>
        Task<bool> WriteAsync(string address, string dataType, object value, int dataLength);

        #endregion

        #region ===================== 日志 =====================

        /// <summary> 记录日志 </summary>
        void Log(string message, LogLevel level = LogLevel.Info);

        #endregion
    }
}
