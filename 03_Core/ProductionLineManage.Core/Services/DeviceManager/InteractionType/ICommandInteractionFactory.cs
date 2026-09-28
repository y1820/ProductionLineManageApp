using ProductionLineManage.Core.Services.DeviceManager.Connection;

namespace ProductionLineManage.Core.Services.DeviceManager.InteractionType
{
    /// <summary>
    /// 创建指令型交互。实现放在 Host，避免共享 Services 认识某一条产线。
    /// </summary>
    public interface ICommandInteractionFactory
    {
        IInteractionType Create(IDeviceTaskContext context);
    }
}
