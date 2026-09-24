using ProductionLineManage.Core.Models.Device;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionLineManage.Core.Services.DeviceManager
{
    /// <summary>
    /// 设备管理服务门面接口：对外统一入口，隐藏连接、业务、状态等内部细节。
    /// </summary>
    public interface IDeviceManagementService
    {
        #region ===================== 设备启动控制 =====================

        /// <summary> 启动指定工位设备连接与交互 </summary>
        Task<bool> StartDeviceAsync(int stationId);

        /// <summary> 停止指定工位设备连接与交互 </summary>
        Task<bool> StopDeviceAsync(int stationId);

        /// <summary> 启动所有已启用工位 </summary>
        Task<bool> StartAllDevicesAsync();

        /// <summary> 停止所有工位 </summary>
        Task<bool> StopAllDevicesAsync();

        #endregion

        #region ===================== 状态查询 =====================

        /// <summary> 获取单个工位实时状态（供 UI 调用） </summary>
        DeviceStatus? GetDeviceStatus(int stationId);

        /// <summary> 获取所有工位实时状态 </summary>
        IReadOnlyDictionary<int, DeviceStatus> GetAllDeviceStatus();

        #endregion

        #region ===================== 状态订阅 =====================

        /// <summary> 工位状态变更事件（供 UI 绑定刷新） </summary>
        event EventHandler<int>? DeviceStatusChanged;

        #endregion
    }
}
