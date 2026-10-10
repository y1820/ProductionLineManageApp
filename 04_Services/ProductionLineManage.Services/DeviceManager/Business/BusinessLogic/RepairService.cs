// ProductionLineManage.Services/RepairService.cs
using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.Device;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.DeviceManager;
using ProductionLineManage.Core.Services.DeviceManager.Business;
using ProductionLineManage.Core.Services.DeviceManager.Connection;
using ProductionLineManage.Core.Services.RepositoryGrop;

using ProductionLineManage.Infrastructure.Data.Repository;
using ProductionLineManage.Infrastructure.Logging;
using ProductionLineManage.Services.Production;

namespace ProductionLineManage.Services.DeviceManager.Business.BusinessLogic
{
    /// <summary>
    /// 返修服务实现：查询允许返修顺序、确认返修目标工位、创建返修待过站记录。
    /// 同时作为 IDeviceDataHandler 供 Interaction 在返修确认时调用。
    /// </summary>
    public class RepairService : IRepairService, IDeviceDataHandler
    {
        #region ===================== 私有字段 =====================

        private readonly IRepository<craft_ProcessInfo> _processInfoRepo;
        private readonly IRepository<production_ProductStationStatus> _statusRepo;
        private readonly SQLHelper _sqlHelper;
        private readonly IRepository<craft_StationInfo> _stationInfoRepo;
        private readonly IDataCacheService _cacheService;
        private readonly ILogger _logger;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入仓储、缓存与日志 </summary>
        public RepairService(
            IRepository<craft_ProcessInfo> processInfoRepo,
            IRepository<production_ProductStationStatus> statusRepo,
            SQLHelper sqlHelper,
            IRepository<craft_StationInfo> stationInfoRepo,
            IDataCacheService cacheService,
            ILogger logger)
        {
            _processInfoRepo = processInfoRepo;
            _statusRepo = statusRepo;
            _sqlHelper = sqlHelper;
            _stationInfoRepo = stationInfoRepo;
            _cacheService = cacheService;
            _logger = logger;
        }

        #endregion

        #region ===================== IDeviceDataHandler =====================

        /// <summary> 处理器标识，供 CommandInteractionType 路由 </summary>
        public string DataType => DeviceHandlerKeys.Repair;

        /// <summary>
        /// 返修确认入口。message.Data 应为 <see cref="RepairConfirmPayload"/>。
        /// </summary>
        public async Task<BusinessResponse> HandleAsync(DeviceDataMessage message, IDeviceTaskContext context)
        {
            var stationId = message.StationId;

            if (message.Data is RepairQueryPayload querPayload)//如果是查询可返修工位业务
            {
               var (success, allowSequence, repairCount, Message) = await QueryAllowRepairSequenceAsync(
                    querPayload.FlowCode, 
                    querPayload.ProductTypeId,
                    querPayload.LineId);
                return new BusinessResponse()
                {
                    Success = success,
                    Data = new RepairQueryResult()
                    {
                        AllowSequence = allowSequence,
                        RepairCount = repairCount,
                    },
                    Message = Message,
                    StationId = message.StationId
                };
            }

            if (message.Data is not RepairConfirmPayload payload)
            {
                context.Log("<HandleAsync> 失败：缺少 RepairConfirmPayload", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = "返修缺少 RepairConfirmPayload 参数"
                };
            }

            context.Log($"<HandleAsync> 开始，流水码={payload.FlowCode}，请求顺序={payload.TargetRepairSequence}，允许顺序={payload.AllowRepairSequence}");

            if (payload.TargetRepairSequence > 0)
            {
                var (success, stationRecordId, targetStationId, repairCount, confirmMessage) =
                    await ConfirmRepairAtSequenceAsync(
                        payload.FlowCode,
                        payload.TrayCode,
                        payload.ProductTypeId,
                        payload.LineId,
                        payload.TargetRepairSequence,
                        payload.AllowRepairSequence,
                        payload.RepairCount);

                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = success,
                    Message = success ? "返修确认成功" : confirmMessage,
                    Data = success
                        ? new RepairConfirmResult
                        {
                            StationRecordId = stationRecordId,
                            TargetStationId = targetStationId,
                            RepairCount = repairCount
                        }
                        : null
                };
            }

            // 旧版接口：按目标工位 Id 确认返修
            context.Log("<HandleAsync> 执行中，调用 ConfirmRepair（旧版 StationId）...");
            var legacySuccess = await ConfirmRepairAsync(
                payload.FlowCode,
                payload.TrayCode,
                stationId,
                payload.TargetStationId);

            var response = new BusinessResponse
            {
                StationId = stationId,
                Success = legacySuccess,
                Message = legacySuccess ? "返修确认成功" : "返修确认失败"
            };

            context.Log($"<HandleAsync> 完成，结果={legacySuccess}，{response.Message}");
            return response;
        }

        #endregion

        #region ===================== 公共方法 =====================

        /// <summary> 获取可返修的工位列表（当前工位之后的所有工位） </summary>
        public async Task<List<craft_StationInfo>> GetAvailableRepairStationsAsync(int productTypeId, int currentStationId)
        {
            try
            {
                // 1. 获取该型号的工艺流程（按 Sequence 排序）
                var processFlow = await GetProcessFlowAsync(productTypeId);
                if (processFlow == null || !processFlow.Any())
                    return new List<craft_StationInfo>();

                // 2. 找到当前工位在流程中的位置
                var currentIndex = processFlow.FindIndex(p => p.StationId == currentStationId);
                if (currentIndex < 0)
                    return new List<craft_StationInfo>();

                // 3. 获取当前工位之后的所有工位 ID（向后跳转，不包括当前）
                var targetStationIds = processFlow
                    .Skip(currentIndex + 1)
                    .Select(p => p.StationId)
                    .ToList();

                if (!targetStationIds.Any())
                    return new List<craft_StationInfo>();

                // 4. 获取工位详细信息
                return await GetStationInfosAsync(targetStationIds);
            }
            catch (Exception ex)
            {
                return new List<craft_StationInfo>();
            }
        }

        /// <summary> 查询允许返修的最大工艺顺序及当前返修次数 </summary>
        public async Task<(bool success, int allowSequence, int repairCount, string message)> QueryAllowRepairSequenceAsync(
            string flowCode, int productTypeId, int lineId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(flowCode))
                    return (false, 0, 0, "流水码为空");

                var records = await GetStationStatusesForFlowAsync(flowCode, productTypeId, lineId);
                if (records.Count == 0)
                {
                    _logger.DeviceLog(0, "", $"<QueryAllowRepairSequence> 流水码 {flowCode} 无任何过站记录");
                    return (false, 0, 0, "无过站记录");
                }

                var processFlow = GetProcessFlow(productTypeId, lineId);
                if (processFlow.Count == 0)
                    return (false, 0, 0, "工艺流程未配置");

                var withSequence = records
                    .Select(r =>
                    {
                        var process = processFlow.FirstOrDefault(p => p.StationId == r.StationId);
                        return new { Record = r, Process = process, Sequence = process?.Sequence ?? 0 };
                    })
                    .Where(x => x.Process != null && x.Record.StationId > 0)
                    .ToList();

                if (withSequence.Count == 0)
                {
                    _logger.DeviceLog(0, "", $"<QueryAllowRepairSequence> 流水码 {flowCode} 无有效工位过站记录");
                    return (false, 0, 0, "无有效工位过站记录");
                }

                var latest = withSequence
                    .OrderByDescending(x => x.Sequence)
                    .ThenByDescending(x => x.Record.UpdateTime ?? x.Record.CreateTime)
                    .ThenByDescending(x => x.Record.Id)
                    .First();

                // 最新工位合格则允许返回到该顺序，否则允许返回到上一顺序
                var allowSequence = latest.Record.Status == 1
                    ? latest.Sequence
                    : latest.Sequence - 1;
                if (allowSequence < 0)
                    allowSequence = 0;

                var repairCount = records.Max(r => r.RepairCount);

                _logger.DeviceLog(0, "", $"<QueryAllowRepairSequence> 流水码={flowCode}，最新顺序={latest.Sequence}，Status={latest.Record.Status}，AllowSeq={allowSequence}，RepairCount={repairCount}");
                return (true, allowSequence, repairCount, "查询成功");
            }
            catch (Exception ex)
            {
                _logger.DeviceLog(0, "", $"<QueryAllowRepairSequence> 异常: {ex.Message}", LogLevel.Error);
                return (false, 0, 0, ex.Message);
            }
        }

        /// <summary> 按工艺顺序确认返修，创建目标工位待过站记录 </summary>
        public async Task<(bool success, int stationRecordId, int targetStationId, int repairCount, string message)> ConfirmRepairAtSequenceAsync(
            string flowCode,
            string trayCode,
            int productTypeId,
            int lineId,
            int requestSequence,
            int allowSequence,
            int currentRepairCount)
        {
            try
            {
                if (requestSequence <= 0)
                    return (false, 0, 0, currentRepairCount, "请求返修顺序无效");

                if (allowSequence <= 0 || requestSequence > allowSequence)
                    return (false, 0, 0, currentRepairCount, "请求返修顺序超出允许范围");

                var processFlow = GetProcessFlow(productTypeId, lineId);
                var targetProcess = processFlow.FirstOrDefault(p => p.Sequence == requestSequence);
                if (targetProcess == null)
                    return (false, 0, 0, currentRepairCount, $"顺序 {requestSequence} 无对应工位");

                var targetStationId = targetProcess.StationId;
                var newRepairCount = currentRepairCount + 1;

                var stationRecordId = await CreateOrResetRepairPendingRecordAsync(
                    flowCode, trayCode, productTypeId, lineId, targetStationId, newRepairCount);

                if (stationRecordId <= 0)
                    return (false, 0, targetStationId, newRepairCount, "创建返修待过站记录失败");

                _logger.DeviceLog(0, "", $"<ConfirmRepairAtSequence> 成功，目标工位={targetStationId}，顺序={requestSequence}，RepairCount={newRepairCount}，过站Id={stationRecordId}");
                return (true, stationRecordId, targetStationId, newRepairCount, "返修确认成功");
            }
            catch (Exception ex)
            {
                _logger.DeviceLog(0, "", $"<ConfirmRepairAtSequence> 异常: {ex.Message}", LogLevel.Error);
                return (false, 0, 0, currentRepairCount, ex.Message);
            }
        }

        /// <summary> 确认返修（旧版：按目标工位 Id） </summary>
        public async Task<bool> ConfirmRepairAsync(string flowCode, string trayCode,
            int currentStationId, int targetStationId)
        {
            try
            {
                // 1. 验证目标工位是否合法（必须是在当前工位之后）
                var isValid = await IsValidRepairTargetAsync(flowCode, trayCode, currentStationId, targetStationId);
                if (!isValid)
                {
                    _logger.DeviceLog(currentStationId, "", "<ConfirmRepair> 返修目标工位不合法");
                    return false;
                }

                await MarkCurrentStationAsRepairAsync(flowCode, trayCode, currentStationId, targetStationId);
                await CreatePendingStationRecordAsync(flowCode, trayCode, targetStationId, true);

                _logger.DeviceLog(currentStationId, "", $"<ConfirmRepair> 返修确认成功，目标工位={targetStationId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.DeviceLog(currentStationId, "", $"<ConfirmRepair> 返修确认异常: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary> 获取产品当前应加工的工位（考虑返修跳转） </summary>
        public async Task<int> GetCurrentProcessingStationAsync(string flowCode, string trayCode, int productTypeId)
        {
            try
            {
                // 1. 查询是否有返修跳转记录
                var repairJump = await GetLatestRepairJumpRecordAsync(flowCode, trayCode);
                if (repairJump != null && repairJump.RepairTargetStationId > 0)
                {
                    return repairJump.RepairTargetStationId; // 有返修跳转，返回目标工位
                }

                // 2. 没有返修，查询最后一条过站记录
                var lastRecord = await GetLatestStationRecordAsync(flowCode, trayCode);
                if (lastRecord == null)
                {
                    return await GetFirstStationAsync(productTypeId); // 没有记录，返回第一个工位
                }

                // 3. 返回下一个工位
                return await GetNextStationAsync(productTypeId, lastRecord.StationId);
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        /// <summary> 检查产品是否处于返修跳转状态 </summary>
        public async Task<bool> IsInRepairJumpAsync(string flowCode, string trayCode)
        {
            var repairJump = await GetLatestRepairJumpRecordAsync(flowCode, trayCode);
            return repairJump != null && repairJump.RepairTargetStationId > 0;
        }

        #endregion

        #region ===================== 私有方法 =====================

        #region --------------------- 工艺流程 ---------------------

        /// <summary> 从缓存获取工艺流程（按 Sequence 排序） </summary>
        private List<craft_ProcessInfo> GetProcessFlow(int productTypeId, int lineId)
        {
            var allProcess = _cacheService.GetData<List<craft_ProcessInfo>>();
            if (allProcess == null)
                return new List<craft_ProcessInfo>();

            return allProcess
                .Where(p => p.TypeId == productTypeId && p.LineId == lineId && p.IsEnable)
                .OrderBy(p => p.Sequence)
                .ToList();
        }

        /// <summary> 获取工艺流程（优先缓存，否则查库） </summary>
        private async Task<List<craft_ProcessInfo>> GetProcessFlowAsync(int productTypeId)
        {
            var allProcess = _cacheService.GetData<List<craft_ProcessInfo>>();
            if (allProcess != null)
            {
                return allProcess
                    .Where(p => p.TypeId == productTypeId)
                    .OrderBy(p => p.Sequence)
                    .ToList();
            }

            var sql = @"
                SELECT * FROM craft_ProcessInfo 
                WHERE TypeId = @ProductTypeId 
                ORDER BY Sequence";

            var result = await _processInfoRepo.QueryAsync<craft_ProcessInfo>(sql, new { ProductTypeId = productTypeId });
            return result?.ToList() ?? new List<craft_ProcessInfo>();
        }

        /// <summary> 获取工位信息列表 </summary>
        private async Task<List<craft_StationInfo>> GetStationInfosAsync(List<int> stationIds)
        {
            var allStations = _cacheService.GetData<List<craft_StationInfo>>();
            if (allStations != null)
            {
                return allStations.Where(s => stationIds.Contains(s.Id)).ToList();
            }

            if (!stationIds.Any()) return new List<craft_StationInfo>();

            var ids = string.Join(",", stationIds);
            var sql = $"SELECT * FROM craft_StationInfo WHERE Id IN ({ids})";
            var result = await _stationInfoRepo.QueryAsync<craft_StationInfo>(sql);
            return result?.ToList() ?? new List<craft_StationInfo>();
        }

        /// <summary> 获取第一个工位 </summary>
        private async Task<int> GetFirstStationAsync(int productTypeId)
        {
            var processFlow = await GetProcessFlowAsync(productTypeId);
            return processFlow.FirstOrDefault()?.StationId ?? 0;
        }

        /// <summary> 获取下一个工位 </summary>
        private async Task<int> GetNextStationAsync(int productTypeId, int currentStationId)
        {
            var processFlow = await GetProcessFlowAsync(productTypeId);
            var currentIndex = processFlow.FindIndex(p => p.StationId == currentStationId);

            if (currentIndex < 0 || currentIndex + 1 >= processFlow.Count)
                return 0; // 没有下一个工位

            return processFlow[currentIndex + 1].StationId;
        }

        #endregion

        #region --------------------- 过站记录 ---------------------

        /// <summary> 查询流水码的所有工位状态记录 </summary>
        private async Task<List<production_ProductStationStatus>> GetStationStatusesForFlowAsync(
            string flowCode, int productTypeId, int lineId)
        {
            const string sql = @"
                SELECT * FROM production_ProductStationStatus
                WHERE FlowCode = @FlowCode
                  AND ProductTypeId = @ProductTypeId
                  AND LineId = @LineId
                ORDER BY UpdateTime DESC, Id DESC";

            var result = await _statusRepo.QueryAsync<production_ProductStationStatus>(sql, new
            {
                FlowCode = flowCode,
                ProductTypeId = productTypeId,
                LineId = lineId
            });

            return result?.ToList() ?? new List<production_ProductStationStatus>();
        }

        /// <summary> 创建或重置返修目标工位的待过站记录 </summary>
        private async Task<int> CreateOrResetRepairPendingRecordAsync(
            string flowCode,
            string trayCode,
            int productTypeId,
            int lineId,
            int targetStationId,
            int repairCount)
        {
            await StationRecordOperations.UpsertRepairTargetStatusAsync(
                _sqlHelper, flowCode, trayCode, targetStationId, productTypeId, lineId, repairCount);

            await StationRecordOperations.CreateOrRefreshOpenPassRecordAsync(
                _sqlHelper, flowCode, trayCode, targetStationId, productTypeId, lineId);

            return await StationRecordOperations.GetProductStationStatusIdAsync(
                _sqlHelper, flowCode, targetStationId, productTypeId, lineId);
        }

        /// <summary> 创建待过站记录 </summary>
        private async Task CreatePendingStationRecordAsync(string flowCode, string trayCode,
            int stationId, bool isRepair = false)
        {
            var productInfo = await GetProductInfoAsync(flowCode, trayCode);
            if (productInfo == null || !productInfo.HasValue)
                return;

            if (isRepair)
            {
                await StationRecordOperations.UpsertRepairTargetStatusAsync(
                    _sqlHelper, flowCode, trayCode, stationId,
                    productInfo.Value.ProductTypeId, productInfo.Value.LineId, 0);
            }
            else
            {
                await StationRecordOperations.UpsertPendingProductStatusAsync(
                    _sqlHelper, flowCode, trayCode, stationId,
                    productInfo.Value.ProductTypeId, productInfo.Value.LineId);
            }
        }

        /// <summary> 查询最新返修跳转记录 </summary>
        private async Task<production_ProductStationStatus?> GetLatestRepairJumpRecordAsync(string flowCode, string trayCode)
        {
            var sql = @"
                SELECT TOP 1 * FROM production_ProductStationStatus 
                WHERE FlowCode = @FlowCode 
                  AND IsRepair = 1 
                  AND RepairTargetStationId > 0
                  AND Status = 0
                ORDER BY UpdateTime DESC, Id DESC";

            return await _statusRepo.QuerySingleAsync<production_ProductStationStatus>(sql, new { FlowCode = flowCode });
        }

        /// <summary> 查询最新过站记录 </summary>
        private async Task<production_ProductStationStatus?> GetLatestStationRecordAsync(string flowCode, string trayCode)
        {
            var sql = @"
                SELECT TOP 1 * FROM production_ProductStationStatus 
                WHERE FlowCode = @FlowCode 
                ORDER BY UpdateTime DESC, Id DESC";

            return await _statusRepo.QuerySingleAsync<production_ProductStationStatus>(sql, new { FlowCode = flowCode });
        }

        #endregion

        #region --------------------- 返修校验 ---------------------

        /// <summary> 验证返修目标是否合法（须在当前工位之后的工位列表中） </summary>
        private async Task<bool> IsValidRepairTargetAsync(string flowCode, string trayCode,
            int currentStationId, int targetStationId)
        {
            var productTypeId = await GetProductTypeIdByFlowCodeAsync(flowCode);
            if (productTypeId == 0) return false;

            var availableStations = await GetAvailableRepairStationsAsync(productTypeId, currentStationId);
            return availableStations.Any(s => s.Id == targetStationId);
        }

        /// <summary> 根据流水码获取产品型号 Id </summary>
        private async Task<int> GetProductTypeIdByFlowCodeAsync(string flowCode)
        {
            var sql = @"
                SELECT TOP 1 ProductTypeId 
                FROM production_ProductStationStatus 
                WHERE FlowCode = @FlowCode 
                ORDER BY CreateTime DESC";

            var record = await _statusRepo.QuerySingleAsync<production_ProductStationStatus>(sql, new { FlowCode = flowCode });
            return record?.ProductTypeId ?? 0;
        }

        /// <summary> 标记当前工位为返修状态 </summary>
        private async Task MarkCurrentStationAsRepairAsync(string flowCode, string trayCode,
            int currentStationId, int targetStationId)
        {
            var sql = @"
                UPDATE production_ProductStationStatus 
                SET IsRepair = 1, 
                    RepairTargetStationId = @TargetStationId,
                    Status = 2,
                    UpdateTime = GETDATE()
                WHERE FlowCode = @FlowCode 
                  AND TrayCode = @TrayCode 
                  AND StationId = @StationId
                  AND Status = 2
                  AND IsRepair = 0";

            await _statusRepo.ExecuteAsync(sql, new
            {
                FlowCode = flowCode,
                TrayCode = trayCode,
                StationId = currentStationId,
                TargetStationId = targetStationId
            });
        }

        /// <summary> 根据流水码获取产品型号与产线 Id </summary>
        private async Task<(int ProductTypeId, int LineId)?> GetProductInfoAsync(string flowCode, string trayCode)
        {
            var sql = @"
                SELECT TOP 1 ProductTypeId, LineId 
                FROM production_ProductStationStatus 
                WHERE FlowCode = @FlowCode 
                ORDER BY UpdateTime DESC, Id DESC";

            var record = await _statusRepo.QuerySingleAsync<production_ProductStationStatus>(
                sql, new { FlowCode = flowCode });
            if (record == null)
                return null;

            return (record.ProductTypeId, record.LineId);
        }

        #endregion

        #endregion
    }
}
