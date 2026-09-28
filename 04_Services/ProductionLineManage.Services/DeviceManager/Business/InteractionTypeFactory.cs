using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.DeviceManager.InteractionType;
using ProductionLineManage.Services.DeviceManager.InteractionType;

namespace ProductionLineManage.Services.DeviceManager.Business
{
    /// <summary>
    /// 交互类型工厂：Signal 在本工厂创建，Command 交给 ICommandInteractionFactory
    /// 在 DeviceConnectionTask.StartAsync 采集线程启动前调用，注入 Mediator 全局 Handler 字典。
    /// </summary>
    public class InteractionTypeFactory : IInteractionTypeFactory
    {
        #region ===================== 字段 =====================

        /// <summary> 业务处理器中介（全局 Handler 注册表） </summary>
        private readonly IDeviceBusinessMediator _mediator;

        /// <summary> 工位看板状态 </summary>
        private readonly IDeviceStatusManager _statusManager;

        /// <summary> 型号/规则等缓存 </summary>
        private readonly IDataCacheService _cacheService;

        /// <summary> 物料校验 </summary>
        private readonly IMaterialService _materialService;
        /// <summary> 指令交互类型工厂 </summary>
        private readonly ICommandInteractionFactory _commandInteraction;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入交互逻辑所需的全部业务服务 </summary>
        public InteractionTypeFactory(
            IDeviceBusinessMediator mediator,
            IDeviceStatusManager statusManager,
            IDataCacheService cacheService,
            IMaterialService materialService,
            ICommandInteractionFactory commandInteraction)
        {
            _mediator = mediator;
            _statusManager = statusManager;
            _cacheService = cacheService;
            _materialService = materialService;
            _commandInteraction = commandInteraction;
        }

        #endregion

        #region ===================== 创建交互逻辑 =====================

        /// <summary> 按 LogicType 创建 IInteractionType 实例（Signal 信号线 / 默认 Command 指令线） </summary>
        public IInteractionType Create(string logicType, IDeviceTaskContext context)
        {
            var handlers = _mediator.GetHandlers(); // 全局 Handler，靠 message.StationId 区分工位

            return logicType switch
            {
                InteractionTypeConstants.Signal => new SignalLineLogic(context, _statusManager, handlers, _cacheService, _materialService),
                _ => _commandInteraction.Create(context)// 非 Signal 均走指令线
            };
        }

        #endregion
    }
}
