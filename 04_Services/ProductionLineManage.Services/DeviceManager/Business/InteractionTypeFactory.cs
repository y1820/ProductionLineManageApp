using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.DeviceManager.InteractionType;

namespace ProductionLineManage.Services.DeviceManager.Business
{
    /// <summary>
    /// 交互类型工厂：当前现场未使用信号型，一律交给 ICommandInteractionFactory。
    /// SignalLineLogic 仍留在工程里，完全分离后再接回。
    /// </summary>
    public class InteractionTypeFactory : IInteractionTypeFactory
    {
        private readonly ICommandInteractionFactory _commandInteraction;

        public InteractionTypeFactory(ICommandInteractionFactory commandInteraction)
        {
            _commandInteraction = commandInteraction;
        }

        /// <summary> 创建工位交互实例。logicType 暂不区分，信号型配置也会走指令交互。 </summary>
        public IInteractionType Create(string logicType, IDeviceTaskContext context)
        {
            return _commandInteraction.Create(context);
        }
    }
}
