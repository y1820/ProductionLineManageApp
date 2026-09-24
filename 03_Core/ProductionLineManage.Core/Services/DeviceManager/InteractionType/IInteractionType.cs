using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DeviceManager.Connection;

namespace ProductionLineManage.Core.Services.DeviceManager.InteractionType
{
    /// <summary>
    /// 工位交互类型接口（指令型 / 信号型）。
    /// 职责：接收 Mediator 转发的 PLC 消息，结合 StationWorkState 判断是否触发业务，并通过 Context 写回 PLC。
    /// 不负责：PLC 连接/采集（由 DeviceConnectionTask 负责）；UI 状态（由 IDeviceStatusManager 更新）。
    /// </summary>
    public interface IInteractionType
    {
        #region ===================== 交互标识 =====================

        /// <summary> 所属工位 Id（与 device_ConnectInfo.StationId 一致） </summary>
        int StationId { get; }

        /// <summary> 逻辑类型：指令类型 / 信号类型（见 LogicTypeConstants） </summary>
        string LogicType { get; }

        #endregion

        #region ===================== 消息处理 =====================

        /// <summary> 处理一条来自 PLC 的数据变化（采集线程或 OPC 订阅回调均走此入口） </summary>
        Task HandleMessageAsync(DeviceDataMessage message, IDeviceTaskContext context);

        #endregion
    }
}
