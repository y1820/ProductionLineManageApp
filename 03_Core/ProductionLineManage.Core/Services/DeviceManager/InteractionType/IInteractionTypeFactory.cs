using ProductionLineManage.Core.Services.DeviceManager.Connection;

namespace ProductionLineManage.Core.Services.DeviceManager.InteractionType
{
    /// <summary>
    /// 交互类型工厂接口。
    /// 由 DeviceConnectionTask 在 StartAsync 时调用，根据 LogicType 创建对应实例。
    /// StationWorkState 在实现类内部 new，不交给 Mediator 管理。
    /// </summary>
    public interface IInteractionTypeFactory
    {
        #region ===================== 实例创建 =====================

        /// <summary> 创建与工位绑定的交互类型实例（每个工位连接任务独立一份） </summary>
        IInteractionType Create(string logicType, IDeviceTaskContext context);

        #endregion
    }
}
