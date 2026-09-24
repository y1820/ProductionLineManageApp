using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.DeviceManager.InteractionType;
using ProductionLineManage.Core.Services.MotorCode;
using ProductionLineManage.Services.DeviceManager.InteractionType;

namespace ProductionLineManage.Services.DeviceManager.Business
{
    /// <summary>
    /// 交互类型工厂：根据 device_ConnectInfo.LogicType 创建 SignalLineLogic / CommandLineLogic。
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

        /// <summary> 流水码校验 </summary>
        private readonly IFlowCodeService _flowCodeService;

        /// <summary> 返修逻辑 </summary>
        private readonly IRepairService _repairService;

        /// <summary> 电机码下发 </summary>
        private readonly IMotorCodeDispatchService _motorCodeDispatch;

        /// <summary> 工位间传值 </summary>
        private readonly IStationDataTransferService _stationDataTransfer;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入交互逻辑所需的全部业务服务 </summary>
        public InteractionTypeFactory(
            IDeviceBusinessMediator mediator,
            IDeviceStatusManager statusManager,
            IDataCacheService cacheService,
            IMaterialService materialService,
            IFlowCodeService flowCodeService,
            IRepairService repairService,
            IMotorCodeDispatchService motorCodeDispatch,
            IStationDataTransferService stationDataTransfer)
        {
            _mediator = mediator;
            _statusManager = statusManager;
            _cacheService = cacheService;
            _materialService = materialService;
            _flowCodeService = flowCodeService;
            _repairService = repairService;
            _motorCodeDispatch = motorCodeDispatch;
            _stationDataTransfer = stationDataTransfer;
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
                _ => new CommandLineLogic(context, _statusManager, handlers, _cacheService, _materialService, _flowCodeService, _repairService, _motorCodeDispatch, _stationDataTransfer), // 非 Signal 均走指令线
            };
        }

        #endregion
    }
}
