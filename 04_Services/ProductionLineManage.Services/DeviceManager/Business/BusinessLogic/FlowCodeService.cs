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
using System.Windows.Interop;

namespace ProductionLineManage.Services.DeviceManager.Business.BusinessLogic
{
    /// <summary>
    /// 流水码验证服务：验证流水码格式、判断上工位是否合格、管理过站记录。
    /// 同时作为 IDeviceDataHandler，供 CommandLineLogic 在请求码 200 时调用。
    /// </summary>
    public class FlowCodeService : IFlowCodeService, IDeviceDataHandler
    {
        #region ===================== 私有字段 =====================

        private readonly IRepository<production_ProductStationStatus> _statusRepo;
        private readonly IDataCacheService _cacheService;
        private readonly IRuleValidation _ruleValidation;
        private readonly SQLHelper _sqlHelper;
        private readonly ILogger _logger;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入仓储、缓存、规则校验与日志 </summary>
        public FlowCodeService(
            IRepository<production_ProductStationStatus> statusRepo,
            IDataCacheService cacheService,
            IRuleValidation ruleValidation,
            ILogger logger,
            SQLHelper sqlHelper)
        {
            _statusRepo = statusRepo;
            _cacheService = cacheService;
            _ruleValidation = ruleValidation;
            _sqlHelper = sqlHelper;
            _logger = logger;
        }

        #endregion

        #region ===================== IDeviceDataHandler =====================

        /// <summary> 处理器标识，供 CommandLineLogic 路由 </summary>
        public string DataType => DeviceHandlerKeys.FlowCode;

        /// <summary>
        /// 流水码验证入口（由 CommandLineLogic 在收到 PLC 请求码 200 时触发）。
        /// message.Data 应为 <see cref="FlowCodeVerifyPayload"/>，型号/产线由编排层传入。
        /// </summary>
        public async Task<BusinessResponse> HandleAsync(DeviceDataMessage message, IDeviceTaskContext context)
        {
            var stationId = message.StationId;

            if (message.Data is not FlowCodeVerifyPayload payload) // 是否传入了消息类
            {
                context.Log("<HandleAsync> 失败：缺少 FlowCodeVerifyPayload", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = "流水码验证缺少 FlowCodeVerifyPayload 参数"
                };
            }

            var flowCode = payload.FlowCode;
            var productTypeId = payload.ProductTypeId;
            var lineId = payload.LineId;

            context.Log($"<HandleAsync> 开始验证，流水码={flowCode}，型号Id={productTypeId}，产线Id={lineId}");

            if (productTypeId <= 0) // 判断型号 Id 是否有效
            {
                var msg = "产品型号Id无效，无法验证流水码";
                context.Log($"<HandleAsync> 中止：{msg}", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = msg
                };
            }

            if (lineId <= 0) // 判断产线 Id 是否有效
            {
                var msg = "产线Id无效，无法验证流水码";
                context.Log($"<HandleAsync> 中止：{msg}", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = msg
                };
            }

            if (payload.Mode == FlowCodeVerifyMode.RulesOnly)//只校验规则，返修使用
            {
                var result = await ValidateFlowCodeRulesOnlyAsync(flowCode, stationId, productTypeId, lineId);
                return new BusinessResponse()
                {
                    StationId = stationId,
                    Success = result,
                    Message = $"<HandleAsync> 验证{(result ? "成功" : "失败")}",
                };
            }

            // 验证编码规则 + 上工位是否合格 + 创建/更新过站记录
            var (success, stationRecordId, isRepair, repairCount, repairTargetStationId) = await ValidateFlowCodeWithRepairAsync(
                flowCode, stationId, productTypeId, lineId, payload.TrayCode);

            var response = new BusinessResponse
            {
                StationId = stationId,
                Success = success,
                Message = success ? "流水码验证通过" : "流水码验证失败",
                Data = success
                    ? new FlowCodeVerifyResult
                    {
                        FlowCode = flowCode,
                        StationRecordId = stationRecordId,
                        IsRepair = isRepair,
                        RepairCount = repairCount,
                        RepairTargetStationId = repairTargetStationId
                    }
                    : null
            };

            context.Log($"<HandleAsync> 完成，结果={success}，{response.Message}");
            return response;
        }

        #endregion

        #region ===================== 公共方法 =====================

        /// <summary> 验证流水码是否符合工位规则 + 上工位是否合格 + 创建或更新过站记录 </summary>
        public async Task<(bool success, int stationRecordId)> ValidateFlowCodeAsync(string flowCode, int stationId,
            int productTypeId, int lineId, string trayCode)
        {
            var result = await ValidateFlowCodeWithRepairAsync(flowCode, stationId, productTypeId, lineId, trayCode);
            return (result.success, result.stationRecordId);
        }

        /// <summary> 判断上工位是否合格（Status=1 为合格） </summary>
        public async Task<bool> IsPreviousStationQualifiedAsync(string flowCode, int currentStationId, int productType)
        {
            try
            {
                var allProcess = _cacheService.GetData<List<craft_ProcessInfo>>(); // 获取缓存里的工艺流程
                var process = allProcess?.FirstOrDefault(p => p.StationId == currentStationId); // 筛选本工位的工艺流程

                if (process == null) // 没有工艺流程
                {
                    _logger.DeviceLog(currentStationId, "P", "<IsPreviousStationQualified> 当前工位未配置工艺流程");
                    return false;
                }

                if (process.UpperWorkstationId <= 0) // 没有写上工位
                {
                    _logger.DeviceLog(currentStationId, "P", "<IsPreviousStationQualified> 当前工位为首站或不判断上工位，通过");
                    return true;
                }

                int previousStationId = process.UpperWorkstationId; // 上工位 Id
                var sql = @"
                    SELECT TOP 1 Status 
                    FROM production_ProductStationStatus 
                    WHERE FlowCode = @FlowCode 
                      AND StationId = @StationId 
                      AND ProductTypeId = @ProductTypeId 
                    ORDER BY UpdateTime DESC, Id DESC";

                var status = await _sqlHelper.QuerySingleAsync<int?>(sql, new
                {
                    FlowCode = flowCode,
                    StationId = previousStationId,
                    ProductTypeId = productType
                });

                bool isQualified = status == 1;
                _logger.DeviceLog(currentStationId, "P", $"<IsPreviousStationQualified> 上工位 {previousStationId} 状态: {(isQualified ? "合格" : "不合格")}");

                return isQualified;
            }
            catch (Exception ex)
            {
                _logger.DeviceLog(currentStationId, "P", $"<IsPreviousStationQualified> 查询上工位是否合格异常: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary> 仅验证流水码编码规则，不创建过站记录 </summary>
        public Task<bool> ValidateFlowCodeRulesOnlyAsync(
            string flowCode, int stationId, int productTypeId, int lineId)
        {
            if (string.IsNullOrEmpty(flowCode) || flowCode == "--")
                return Task.FromResult(false);

            var ok = ValidateCodeRulesAsync(flowCode, stationId, productTypeId, lineId);
            return Task.FromResult(ok);
        }

        #endregion

        #region ===================== 私有方法 =====================

        #region --------------------- 核心验证流程 ---------------------

        /// <summary> 完整流水码验证（含返修信息） </summary>
        private async Task<(bool success, int stationRecordId, bool isRepair, int repairCount, int repairTargetStationId)>
            ValidateFlowCodeWithRepairAsync(string flowCode, int stationId, int productTypeId, int lineId, string trayCode)
        {
            if (string.IsNullOrEmpty(flowCode) || flowCode == "--")
            {
                _logger.DeviceLog(stationId, "流水码验证", "<ValidateFlowCode> 条码为空，流水码验证不合格");
                return (false, 0, false, 0, 0);
            }

            try
            {
                // 编码规则验证
                var isRuleValid = ValidateCodeRulesAsync(flowCode, stationId, productTypeId, lineId);
                if (!isRuleValid)
                    return (false, 0, false, 0, 0);

                // 判断上工位是否合格
                var isPreviousStationPass = await IsPreviousStationQualifiedAsync(flowCode, stationId, productTypeId);
                if (!isPreviousStationPass)
                {
                    _logger.DeviceLog(stationId, "过站记录查询", "<ValidateFlowCode> 上工位验证不通过");
                    return (false, 0, false, 0, 0);
                }

                if (!await CanAcceptWorkAtCurrentStationAsync(flowCode, stationId, productTypeId, lineId))
                {
                    _logger.DeviceLog(stationId, "流水码验证", "<ValidateFlowCode> 本工位不允许加工（已合格/不合格/重复加工限制）");
                    return (false, 0, false, 0, 0);
                }

                // 创建或更新过站记录
                var verifyResult = await CreateOrUpdateStationRecordAsync(
                    flowCode, trayCode, stationId, productTypeId, lineId);
                if (verifyResult.StationRecordId <= 0)
                {
                    _logger.DeviceLog(stationId, "流水码验证", "<ValidateFlowCode> 工位状态创建/更新后未获取到 Id", LogLevel.Warning);
                    return (false, 0, false, 0, 0);
                }

                _logger.DeviceLog(stationId, "流水码验证",
                    $"<ValidateFlowCode> 流水码验证通过: {flowCode}，工位状态 Id={verifyResult.StationRecordId}，IsRepair={verifyResult.IsRepair}，RepairCount={verifyResult.RepairCount}");
                return (true, verifyResult.StationRecordId, verifyResult.IsRepair, verifyResult.RepairCount, verifyResult.RepairTargetStationId);
            }
            catch (Exception ex)
            {
                _logger.DeviceLog(stationId, "流水码验证", $"<ValidateFlowCode> 流水码验证异常: {ex.Message}", LogLevel.Error);
                return (false, 0, false, 0, 0);
            }
        }

        /// <summary>
        /// 本工位是否允许加工：未勾选重复加工时 Status=1 拒绝；Status=2（不合格）一律拒绝。
        /// </summary>
        private async Task<bool> CanAcceptWorkAtCurrentStationAsync(
            string flowCode, int stationId, int productTypeId, int lineId)
        {
            var allProcess = _cacheService.GetData<List<craft_ProcessInfo>>();
            var process = allProcess?.FirstOrDefault(p =>
                p.StationId == stationId &&
                p.TypeId == productTypeId &&
                p.LineId == lineId &&
                p.IsEnable);

            if (process == null)
            {
                _logger.DeviceLog(stationId, "P", "<CanAcceptWorkAtCurrentStation> 当前工位未配置工艺流程");
                return false;
            }

            const string sql = @"
                SELECT TOP 1 Status
                FROM production_ProductStationStatus
                WHERE FlowCode = @FlowCode
                  AND StationId = @StationId
                  AND ProductTypeId = @ProductTypeId
                  AND LineId = @LineId
                ORDER BY UpdateTime DESC, Id DESC";

            var status = await _sqlHelper.QuerySingleAsync<int?>(sql, new
            {
                FlowCode = flowCode,
                StationId = stationId,
                ProductTypeId = productTypeId,
                LineId = lineId
            });
            if (process.IsRepeatWork)
                return true;

            if (status == 2)
            {
                _logger.DeviceLog(stationId, "P",
                    "<CanAcceptWorkAtCurrentStation> 本工位最新过站 Status=2（不合格），不允许加工，拒绝 200");
                return false;
            }

           

            if (status == 1)
            {
                _logger.DeviceLog(stationId, "P",
                    "<CanAcceptWorkAtCurrentStation> 本工位最新过站 Status=1 且未允许重复加工，拒绝 200");
                return false;
            }

            return true;
        }

        #endregion

        #region --------------------- 编码规则 ---------------------

        /// <summary> 编码规则验证 </summary>
        private bool ValidateCodeRulesAsync(string flowCode, int stationId,
            int productTypeId, int lineId)
        {
            var rules = GetRulesFromCache(stationId, productTypeId, lineId);

            if (rules == null || rules.Count == 0)
            {
                _logger.DeviceLog(stationId, "返修", "<ValidateCodeRules> 没有编码规则，直接通过");
                return true;
            }

            var (isValid, ruleMessage) = _ruleValidation.ValidateRules(rules, flowCode);

            if (!isValid)
                _logger.DeviceLog(stationId, "返修", $"<ValidateCodeRules> 编码规则验证不通过：{ruleMessage}");

            return isValid;
        }

        /// <summary> 从缓存获取流水码编码规则 </summary>
        private List<craft_FlowCodeRules> GetRulesFromCache(int stationId, int productTypeId, int lineId)
        {
            var allRules = _cacheService.GetData<List<craft_FlowCodeRules>>();
            if (allRules == null)
                return new List<craft_FlowCodeRules>();

            return allRules
                .Where(r => r.StationId == stationId
                         && r.ProductTypeId == productTypeId
                         && r.LineId == lineId
                         && r.IsEnabled)
                .ToList();
        }

        #endregion

        #region --------------------- 过站记录 ---------------------

        /// <summary> 创建或更新工位状态，并刷新过站明细 </summary>
        private async Task<FlowCodeVerifyResult> CreateOrUpdateStationRecordAsync(string flowCode, string trayCode,
            int stationId, int productTypeId, int lineId)
        {
            await StationRecordOperations.UpsertPendingProductStatusAsync(
                _sqlHelper, flowCode, trayCode, stationId, productTypeId, lineId);

            var passRecordId = await StationRecordOperations.CreateOrRefreshOpenPassRecordAsync(
                _sqlHelper, flowCode, trayCode, stationId, productTypeId, lineId);

            var statusRow = await LoadProductStationStatusAsync(flowCode, stationId, productTypeId, lineId);
            var statusId = statusRow?.Id ?? 0;

            _logger.DeviceLog(stationId, "P",
                $"<CreateOrUpdateStationRecord> 工位状态 Id={statusId}，过站明细 Id={passRecordId}，IsRepair={statusRow?.IsRepair}，RepairCount={statusRow?.RepairCount ?? 0}");

            return new FlowCodeVerifyResult
            {
                FlowCode = flowCode,
                StationRecordId = statusId,
                IsRepair = statusRow?.IsRepair ?? false,
                RepairCount = statusRow?.RepairCount ?? 0,
                RepairTargetStationId = statusRow?.RepairTargetStationId ?? 0
            };
        }

        /// <summary> 加载工位状态记录 </summary>
        private async Task<production_ProductStationStatus?> LoadProductStationStatusAsync(
            string flowCode, int stationId, int productTypeId, int lineId)
        {
            const string sql = @"
                SELECT TOP 1 *
                FROM production_ProductStationStatus
                WHERE FlowCode = @FlowCode
                  AND StationId = @StationId
                  AND ProductTypeId = @ProductTypeId
                  AND LineId = @LineId";

            return await _sqlHelper.QuerySingleAsync<production_ProductStationStatus>(sql, new
            {
                FlowCode = flowCode,
                StationId = stationId,
                ProductTypeId = productTypeId,
                LineId = lineId
            });
        }

        #endregion

        #endregion
    }
}
