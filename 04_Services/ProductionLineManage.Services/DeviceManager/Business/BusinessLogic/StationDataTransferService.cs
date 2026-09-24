using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.RepositoryGrop;
using ProductionLineManage.Infrastructure.Logging;
using Prism.Events;

namespace ProductionLineManage.Services.DeviceManager.Business.BusinessLogic
{
    /// <summary>
    /// 工位传值服务：从 report_ProcessHistory 读取源工位（如 OP090 Web 保存）数据，
    /// 供请求工位（如 OP130）在 200 流水码验证通过后写入 PLC。
    /// </summary>
    public class StationDataTransferService : IStationDataTransferService, IDeviceDataHandler
    {
        #region ===================== 私有字段 =====================

        private readonly IRepository<craft_StationDataTransfer> _dataTransferRepo;
        private readonly IRepository<report_ProcessHistory> _processHistoryRepo;
        private readonly IDataCacheService _cacheService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger _logger;

        /// <summary> 启动加载 / DataTransferUpdatedEvent 更新后的配置快照 </summary>
        private volatile IReadOnlyList<craft_StationDataTransfer> _transferConfigs =
            new List<craft_StationDataTransfer>().AsReadOnly();

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入仓储、缓存、事件聚合器与日志，并初始化传值配置 </summary>
        public StationDataTransferService(
            IRepository<craft_StationDataTransfer> dataTransferRepo,
            IRepository<report_ProcessHistory> processHistoryRepo,
            IDataCacheService cacheService,
            IEventAggregator eventAggregator,
            ILogger logger)
        {
            _dataTransferRepo = dataTransferRepo;
            _processHistoryRepo = processHistoryRepo;
            _cacheService = cacheService;
            _eventAggregator = eventAggregator;
            _logger = logger;

            InitializeData();
        }

        #endregion

        #region ===================== IDeviceDataHandler =====================

        /// <summary> 处理器标识，供 CommandLineLogic 路由 </summary>
        public string DataType => DeviceHandlerKeys.StationDataTransfer;

        /// <summary> Handler 入口（可选；200 主路径由 CommandLineLogic 直接调 GetTransferWritesAsync） </summary>
        public async Task<BusinessResponse> HandleAsync(DeviceDataMessage message, IDeviceTaskContext context)
        {
            var stationId = message.StationId;

            if (message.Data is not StationTransferPayload payload)
            {
                context.Log("<工位传值> 失败：缺少 StationTransferPayload", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = "工位传值缺少 StationTransferPayload 参数"
                };
            }

            context.Log($"<工位传值> Handler 开始，流水码={payload.FlowCode}，型号Id={payload.ProductTypeId}，产线Id={payload.LineId}");

            var items = await GetTransferWritesAsync(
                stationId, payload.ProductTypeId, payload.LineId, payload.FlowCode);

            var hasData = items.Count > 0;
            return new BusinessResponse
            {
                StationId = stationId,
                Success = hasData,
                Message = hasData ? "工位传值数据已获取" : "无传值配置或历史数据",
                Data = items
            };
        }

        #endregion

        #region ===================== IStationDataTransferService =====================

        /// <summary> 按配置从源工位历史数据组装待写入 PLC 的传值项 </summary>
        public async Task<IReadOnlyList<StationTransferWriteItem>> GetTransferWritesAsync(
            int requestStationId,
            int productTypeId,
            int lineId,
            string flowCode)
        {
            var result = new List<StationTransferWriteItem>();

            if (string.IsNullOrWhiteSpace(flowCode))
                return result;

            var matchedConfigs = _transferConfigs
                .Where(t => t.RequestStationId == requestStationId
                         && t.ProductTypeId == productTypeId
                         && t.LineId == lineId
                         && t.IsEnabled)
                .ToList();

            if (matchedConfigs.Count == 0)
            {
                _logger.DeviceLog(requestStationId, "", "<工位传值> 无匹配配置，跳过");
                return result;
            }

            foreach (var config in matchedConfigs)
            {
                const string sql = @"
SELECT TOP 1 DataValue
FROM report_ProcessHistory
WHERE FlowCode = @FlowCode
  AND DataName = @DataName
  AND StationId = @SourceStationId
ORDER BY Id DESC";

                var dataValue = await _processHistoryRepo.QuerySingleAsync<string?>(sql, new
                {
                    FlowCode = flowCode,
                    DataName = config.RequestDataName,
                    SourceStationId = config.SourceStationId
                });

                if (string.IsNullOrEmpty(dataValue))
                {
                    _logger.DeviceLog(requestStationId, "",
                        $"<工位传值> 未找到历史数据：DataName={config.RequestDataName}，源工位Id={config.SourceStationId}，FlowCode={flowCode}",
                        LogLevel.Warning);
                    continue;
                }

                result.Add(new StationTransferWriteItem
                {
                    RequestDataName = config.RequestDataName,
                    SourceStationId = config.SourceStationId,
                    DataValue = dataValue,
                    TargetAddress = config.SourceAddress, // 配置中的目标 PLC 地址
                    DataType = config.DataType,
                    DataLength = config.DataLength
                });
            }

            _logger.DeviceLog(requestStationId, "",
                $"<工位传值> 已匹配 {matchedConfigs.Count} 条配置，有效历史数据 {result.Count} 条");
            return result;
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 初始化传值配置快照并订阅更新事件 </summary>
        private void InitializeData()
        {
            if (_cacheService.HasData<List<craft_StationDataTransfer>>())
            {
                _transferConfigs = (_cacheService.GetData<List<craft_StationDataTransfer>>() ?? new()).AsReadOnly();
            }

            _eventAggregator.GetEvent<DataTransferUpdatedEvent>().Subscribe(OnDataTransferUpdated);
        }

        /// <summary> 传值配置更新回调 </summary>
        private void OnDataTransferUpdated(List<craft_StationDataTransfer> list)
        {
            _transferConfigs = (list ?? new()).AsReadOnly();
        }

        #endregion
    }
}
