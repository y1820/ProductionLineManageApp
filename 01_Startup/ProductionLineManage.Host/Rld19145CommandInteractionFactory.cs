using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.DeviceManager.InteractionType;
using ProductionLineManage.Core.Services.MotorCode;
using ProductionLineManage.Line.RLD19145.Interaction;

namespace ProductionLineManage.Host
{
    public class Rld19145CommandInteractionFactory : ICommandInteractionFactory
    {
        private readonly IDeviceStatusManager _statusManager;
        private readonly IDataCacheService _cacheService;
        private readonly IMaterialService _materialService;
        private readonly IFlowCodeService _flowCodeService;
        private readonly IRepairService _repairService;
        private readonly IMotorCodeDispatchService _motorCodeDispatch;
        private readonly IStationDataTransferService _stationDataTransfer;
        private readonly IDeviceBusinessMediator _mediator;
        public Rld19145CommandInteractionFactory(
            IDeviceStatusManager statusManage,
            IDataCacheService cacheService,
            IMaterialService materialService,
            IFlowCodeService flowCodeService,
            IRepairService repairService,
            IMotorCodeDispatchService motorCodeDispatch,
            IStationDataTransferService stationDataTransfer,
            IDeviceBusinessMediator mediator)
        {
            _statusManager = statusManage;
            _cacheService = cacheService;
            _materialService = materialService;
            _flowCodeService = flowCodeService;
            _repairService = repairService;
            _motorCodeDispatch = motorCodeDispatch;
            _stationDataTransfer = stationDataTransfer;
            _mediator = mediator;
        }

        public IInteractionType Create(IDeviceTaskContext context)
        {
            var handlers = _mediator.GetHandlers();
            return new CommandLineLogic(
                context,
                _statusManager,
                handlers,
                _cacheService,
                _materialService,
                _flowCodeService,
                _repairService,
                _motorCodeDispatch,
                _stationDataTransfer);
        }
    }
}
