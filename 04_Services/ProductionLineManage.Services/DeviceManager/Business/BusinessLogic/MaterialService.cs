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

namespace ProductionLineManage.Services.DeviceManager.Business.BusinessLogic
{
    /// <summary>
    /// 物料服务实现：验证物料码、编码规则、工位配置与录入顺序。
    /// 同时作为 IDeviceDataHandler 供 Interaction 在物料验证时调用。
    /// </summary>
    public class MaterialService : IMaterialService, IDeviceDataHandler
    {
        #region ===================== 私有字段 =====================

        private readonly IRepository<material_Station> _stationMaterialRepo;
        private readonly IRepository<report_MaterialBind> _materialBindRepo;
        private readonly IDataCacheService _cacheService;
        private readonly IRuleValidation _ruleValidation;
        private readonly SQLHelper _sqlHelper;
        private readonly ILogger _logger;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入仓储、缓存、规则校验与日志 </summary>
        public MaterialService(
            IRepository<material_Station> stationMaterialRepo,
            IRepository<report_MaterialBind> materialBindRepo,
            IDataCacheService cacheService,
            IRuleValidation ruleValidation,
            SQLHelper sqlHelper,
            ILogger logger)
        {
            _stationMaterialRepo = stationMaterialRepo;
            _materialBindRepo = materialBindRepo;
            _cacheService = cacheService;
            _ruleValidation = ruleValidation;
            _sqlHelper = sqlHelper;
            _logger = logger;
        }

        #endregion

        #region ===================== IDeviceDataHandler =====================

        /// <summary> 处理器标识，供 CommandLineLogic 路由 </summary>
        public string DataType => DeviceHandlerKeys.Material;

        /// <summary>
        /// 物料验证入口。message.Data 应为 <see cref="MaterialVerifyPayload"/>。
        /// </summary>
        public async Task<BusinessResponse> HandleAsync(DeviceDataMessage message, IDeviceTaskContext context)
        {
            var stationId = message.StationId;

            if (message.Data is not MaterialVerifyPayload payload)
            {
                context.Log("<HandleAsync> 失败：缺少 MaterialVerifyPayload", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = "物料验证缺少 MaterialVerifyPayload 参数"
                };
            }

            context.Log($"<HandleAsync> 开始验证，物料码={payload.MaterialCode}，类型={payload.MaterialType}");

            if (payload.ProductTypeId <= 0 || payload.LineId <= 0)
            {
                var msg = payload.ProductTypeId <= 0 ? "产品型号Id无效" : "产线Id无效";
                context.Log($"<HandleAsync> 中止：{msg}", LogLevel.Warning);
                return new BusinessResponse
                {
                    StationId = stationId,
                    Success = false,
                    Message = msg
                };
            }

            var verifyResult = payload.RulesOnly
                ? await ValidateMaterialRulesOnlyAsync(
                    payload.MaterialType,
                    payload.MaterialCode,
                    stationId,
                    payload.ProductTypeId)
                : await ValidateMaterialAsync(
                    payload.MaterialType,
                    payload.MaterialCode,
                    stationId,
                    payload.ProductTypeId,
                    payload.LineId,
                    payload.ValidatedMaterialIds);

            return new BusinessResponse
            {
                StationId = stationId,
                Success = verifyResult.Success,
                Message = verifyResult.Message,
                Data = verifyResult
            };
        }

        #endregion

        #region ===================== 公共方法 =====================

        /// <summary> 完整物料验证：档案 → 编码规则 → 工位配置 → 合格工位 → 父物料 → 录入顺序 </summary>
        public async Task<MaterialVerifyResult> ValidateMaterialAsync(
            string materialType,
            string materialCode,
            int stationId,
            int typeId,
            int lineId,
            IReadOnlyList<int> validatedMaterialIds)
        {
            materialType = materialType?.Trim() ?? string.Empty;
            materialCode = materialCode?.Trim() ?? string.Empty;
            validatedMaterialIds ??= Array.Empty<int>();

            if (string.IsNullOrEmpty(materialType))
            {
                _logger.DeviceLog(stationId, "", "<ValidateMaterial> 物料类型为空");
                return Fail("物料类型为空", materialCode);
            }

            if (string.IsNullOrEmpty(materialCode))
            {
                _logger.DeviceLog(stationId, "", "<ValidateMaterial> 物料码为空");
                return Fail("物料码为空", materialCode);
            }

            // ① 物料档案（物料类型 = material_Info.Code）
            var materialInfo = await GetMaterialInfoByCodeAsync(materialType, typeId);
            if (materialInfo == null)
            {
                _logger.DeviceLog(stationId, "", $"<ValidateMaterial> 物料代号不存在：{materialType}");
                return Fail($"物料代号不存在：{materialType}", materialCode);
            }

            // ② 编码规则
            var (ruleOk, ruleMsg) = await ValidateMaterialCodeRulesAsync(materialCode, typeId, materialInfo.Id, stationId);
            if (!ruleOk)
            {
                _logger.DeviceLog(stationId, "", $"<ValidateMaterial> {ruleMsg}");
                return Fail(ruleMsg, materialCode, isRuleFailure: true);
            }

            // ③ 工位物料配置
            var stationMaterial = await GetStationMaterialAsync(stationId, typeId, lineId, materialInfo.Id);
            if (stationMaterial == null)
            {
                _logger.DeviceLog(stationId, "", $"<ValidateMaterial> 该工位未配置物料：{materialInfo.Name}({materialType})");
                return Fail($"该工位未配置物料：{materialInfo.Name}", materialCode);
            }

            // ④ 物料加工合格工位
            if (stationMaterial.CheckMaterialStationId > 0)
            {
                var checkOk = await IsMaterialProcessedQualifiedAsync(
                    materialCode, stationMaterial.CheckMaterialStationId, stationId);
                if (!checkOk)
                {
                    _logger.DeviceLog(stationId, "",
                        $"<ValidateMaterial> 物料 {materialCode} 未在工位 {stationMaterial.CheckMaterialStationId} 合格加工");
                    return Fail(
                        $"物料未在工位 {stationMaterial.CheckMaterialStationId} 合格加工",
                        materialCode);
                }
            }

            // ⑤ 父物料
            if (stationMaterial.ParentMaterialId > 0 &&
                !validatedMaterialIds.Contains(stationMaterial.ParentMaterialId))
            {
                _logger.DeviceLog(stationId, "",
                    $"<ValidateMaterial> 须先验证父物料 Id={stationMaterial.ParentMaterialId}");
                return Fail("须先验证父物料", materialCode);
            }

            // ⑥ 录入顺序
            if (stationMaterial.Sequence > 0)
            {
                var sequenceError = ValidateSequence(stationId, typeId, lineId, stationMaterial, validatedMaterialIds);
                if (sequenceError != null)
                    return Fail(sequenceError, materialCode);
            }

            _logger.DeviceLog(stationId, "",
                $"<ValidateMaterial> 验证通过：{materialInfo.Name} - {materialCode}");

            return new MaterialVerifyResult
            {
                Success = true,
                Message = "物料验证通过",
                MaterialId = materialInfo.Id,
                MaterialCode = materialCode,
                MaterialName = materialInfo.Name,
                Sequence = stationMaterial.Sequence
            };
        }

        /// <summary> 仅验证物料编码规则，不校验工位配置与录入顺序 </summary>
        public async Task<MaterialVerifyResult> ValidateMaterialRulesOnlyAsync(
            string materialType,
            string materialCode,
            int stationId,
            int typeId)
        {
            materialType = materialType?.Trim() ?? string.Empty;
            materialCode = materialCode?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(materialType))
                return Fail("物料类型为空", materialCode);

            if (string.IsNullOrEmpty(materialCode))
                return Fail("物料码为空", materialCode);

            var materialInfo = await GetMaterialInfoByCodeAsync(materialType, typeId);
            if (materialInfo == null)
                return Fail($"物料代号不存在：{materialType}", materialCode);

            var (ruleOk, ruleMsg) = await ValidateMaterialCodeRulesAsync(materialCode, typeId, materialInfo.Id, stationId);
            if (!ruleOk)
                return Fail(ruleMsg, materialCode, isRuleFailure: true);

            return new MaterialVerifyResult
            {
                Success = true,
                Message = "物料编码规则验证通过",
                MaterialId = materialInfo.Id,
                MaterialCode = materialCode,
                MaterialName = materialInfo.Name
            };
        }

        /// <summary> 按物料代号从缓存查找物料档案 </summary>
        public Task<material_Info?> GetMaterialInfoByCodeAsync(string materialCode, int typeId)
        {
            if (string.IsNullOrWhiteSpace(materialCode))
                return Task.FromResult<material_Info?>(null);

            var code = materialCode.Trim();
            var allMaterials = _cacheService.GetData<List<material_Info>>();
            if (allMaterials == null || allMaterials.Count == 0)
            {
                _logger.DeviceLog(0, "", $"<GetMaterialInfoByCode> 缓存中无物料信息，代号={code}，型号Id={typeId}");
                return Task.FromResult<material_Info?>(null);
            }

            var matched = allMaterials.FirstOrDefault(m =>
                m.TypeId == typeId &&
                string.Equals(m.Code?.Trim(), code, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
                return Task.FromResult<material_Info?>(matched);

            var sameCodeOtherTypes = allMaterials
                .Where(m => string.Equals(m.Code?.Trim(), code, StringComparison.OrdinalIgnoreCase))
                .Select(m => m.TypeId)
                .Distinct()
                .ToList();

            if (sameCodeOtherTypes.Count > 0)
            {
                _logger.DeviceLog(0, "", $"<GetMaterialInfoByCode> 代号 {code} 存在于型号 Id=[{string.Join(",", sameCodeOtherTypes)}]，当前型号 Id={typeId} 未匹配");
            }
            else
            {
                _logger.DeviceLog(0, "", $"<GetMaterialInfoByCode> 缓存中未找到代号 {code}，型号Id={typeId}，缓存物料总数={allMaterials.Count}");
            }

            return Task.FromResult<material_Info?>(null);
        }

        /// <summary> 获取工位配置的物料列表（按 Sequence 排序） </summary>
        public Task<List<material_Station>> GetStationMaterialsAsync(int stationId, int typeId, int lineId)
        {
            var allMaterials = _cacheService.GetData<List<material_Station>>();
            if (allMaterials == null)
                return Task.FromResult(new List<material_Station>());

            var list = allMaterials
                .Where(m => m.StationId == stationId && m.TypeId == typeId && m.LineId == lineId)
                .OrderBy(m => m.Sequence)
                .ToList();

            return Task.FromResult(list);
        }

        /// <summary> 查询指定流水码已绑定的物料 </summary>
        public async Task<List<report_MaterialBind>> GetBoundMaterialsAsync(string flowCode)
        {
            const string sql = @"
                SELECT * FROM report_MaterialBind 
                WHERE FlowCode = @FlowCode AND BindStatus = @BindStatus
                ORDER BY BindTime DESC";

            var result = await _materialBindRepo.QueryAsync<report_MaterialBind>(sql, new
            {
                flowCode,
                BindStatus = BindStatusConstants.Bound
            });

            return result?.ToList() ?? new List<report_MaterialBind>();
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 构造失败结果 </summary>
        private static MaterialVerifyResult Fail(string message, string materialCode, bool isRuleFailure = false) =>
            new()
            {
                Success = false,
                Message = message,
                IsRuleFailure = isRuleFailure,
                MaterialCode = materialCode
            };

        /// <summary> 校验录入顺序：须先验证 Sequence 更小的物料 </summary>
        private string? ValidateSequence(
            int stationId,
            int typeId,
            int lineId,
            material_Station current,
            IReadOnlyList<int> validatedMaterialIds)
        {
            var allStationMaterials = _cacheService.GetData<List<material_Station>>();
            if (allStationMaterials == null)
                return null;

            var prerequisites = allStationMaterials
                .Where(m =>
                    m.StationId == stationId &&
                    m.TypeId == typeId &&
                    m.LineId == lineId &&
                    m.Sequence > 0 &&
                    m.Sequence < current.Sequence)
                .ToList();

            foreach (var prerequisite in prerequisites)
            {
                if (!validatedMaterialIds.Contains(prerequisite.MaterialId))
                {
                    _logger.DeviceLog(stationId, "",
                        $"<ValidateMaterial> 录入顺序错误，须先验证 Sequence={prerequisite.Sequence} 的物料");
                    return $"录入顺序错误，须先验证顺序号为 {prerequisite.Sequence} 的物料";
                }
            }

            return null;
        }

        /// <summary> 检查物料是否在指定工位合格加工（Status=1） </summary>
        private async Task<bool> IsMaterialProcessedQualifiedAsync(
            string materialCode, int checkStationId, int stationId)
        {
            const string sql = @"
                SELECT TOP 1 Status
                FROM production_ProductStationStatus
                WHERE FlowCode = @FlowCode
                  AND StationId = @StationId
                ORDER BY EndTime DESC";

            var status = await _sqlHelper.QuerySingleAsync<int?>(sql, new
            {
                FlowCode = materialCode,
                StationId = checkStationId
            });

            var qualified = status == 1;
            if (!qualified)
            {
                _logger.DeviceLog(stationId, "",
                    $"<IsMaterialProcessedQualified> 物料 {materialCode} 在工位 {checkStationId} 最新过站 Status={status?.ToString() ?? "无记录"}");
            }

            return qualified;
        }

        /// <summary> 获取工位物料配置（优先缓存，否则查库） </summary>
        private async Task<material_Station?> GetStationMaterialAsync(int stationId, int typeId, int lineId, int materialId)
        {
            var allStationMaterials = _cacheService.GetData<List<material_Station>>();
            if (allStationMaterials != null)
            {
                return allStationMaterials.FirstOrDefault(m =>
                    m.StationId == stationId &&
                    m.TypeId == typeId &&
                    m.LineId == lineId &&
                    m.MaterialId == materialId);
            }

            const string sql = @"
                SELECT TOP 1 * FROM material_Station 
                WHERE StationId = @StationId 
                  AND TypeId = @TypeId 
                  AND LineId = @LineId 
                  AND MaterialId = @MaterialId";

            return await _stationMaterialRepo.QuerySingleAsync<material_Station>(sql,
                new { StationId = stationId, TypeId = typeId, LineId = lineId, MaterialId = materialId });
        }

        /// <summary> 验证物料编码规则 </summary>
        private Task<(bool result, string mes)> ValidateMaterialCodeRulesAsync(
            string materialCode, int typeId, int materialId, int stationId)
        {
            var allRules = _cacheService.GetData<List<material_CodeRules>>();
            if (allRules == null || allRules.Count == 0)
            {
                _logger.DeviceLog(stationId, "", "<ValidateMaterialCode> 没有编码规则，直接通过");
                return Task.FromResult((true, string.Empty));
            }

            var rules = allRules
                .Where(r => r.TypeId == typeId && r.MaterialId == materialId && r.IsEnabled)
                .ToList();

            if (rules.Count == 0)
            {
                _logger.DeviceLog(stationId, "", "<ValidateMaterialCode> 该物料无编码规则，直接通过");
                return Task.FromResult((true, string.Empty));
            }

            var (passed, ruleMessage) = _ruleValidation.ValidateRules(rules, materialCode);
            if (!passed)
            {
                _logger.DeviceLog(stationId, "", $"<ValidateMaterialCode> 编码规则验证不通过：{ruleMessage}");
                return Task.FromResult((false, ruleMessage));
            }

            return Task.FromResult((true, string.Empty));
        }

        #endregion
    }
}
