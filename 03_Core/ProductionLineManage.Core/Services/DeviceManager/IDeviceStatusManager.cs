using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.Device;

namespace ProductionLineManage.Core.Services.DeviceManager
{
    /// <summary>
    /// 设备状态管理器接口：线程安全维护各工位面向 UI 的展示状态。
    /// </summary>
    public interface IDeviceStatusManager
    {
        #region ===================== 状态读写 =====================

        /// <summary> 原子更新指定工位状态 </summary>
        void UpdateStatus(int stationId, Action<DeviceStatus> updateAction);

        /// <summary> 读取单个工位状态 </summary>
        DeviceStatus? GetStatus(int stationId);

        /// <summary> 读取所有工位状态 </summary>
        IReadOnlyDictionary<int, DeviceStatus> GetAllStatus();

        /// <summary> 快捷更新连接状态 </summary>
        void UpdateConnectionStatus(int stationId, ConnectionState state);

        #endregion

        #region ===================== 状态订阅 =====================

        /// <summary> 状态变更通知（仅 UI 相关字段变化时触发） </summary>
        event EventHandler<int> StatusChanged;

        #endregion
    }
}
