using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductionLineManage.Core.Services.DeviceManager
{
    /// <summary>
    /// 设备数据处理器接口（业务层实现）：按 DataType 处理 PLC 触发的业务消息。
    /// </summary>
    public interface IDeviceDataHandler
    {
        #region ===================== 处理器标识 =====================

        /// <summary> 处理器对应的数据类型（如 FlowCode、Material、SaveData） </summary>
        string DataType { get; }

        #endregion

        #region ===================== 业务处理 =====================

        /// <summary> 处理一条设备数据消息并返回业务响应 </summary>
        Task<BusinessResponse> HandleAsync(DeviceDataMessage message, IDeviceTaskContext context);

        #endregion
    }
}
