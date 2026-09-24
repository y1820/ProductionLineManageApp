using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.MotorCode;
using ProductionLineManage.Core.Services.MotorCode;
using ProductionLineManage.Infrastructure.Data.Repository;
using ProductionLineManage.Infrastructure.Logging;

namespace ProductionLineManage.Services.MotorCode
{
    /// <summary>
    /// 900 指令下发服务：根据流水码查询最新绑定的电机码，
    /// 并从缓存中取该型号的平台代号固定信息一并返回。
    /// </summary>
    public sealed class MotorCodeDispatchService : IMotorCodeDispatchService
    {
        #region ===================== 常量与字段 =====================

        private const string LogSource = "MotorCodeDispatch";
        private const string DefaultMotorMaterialName = "电机码";

        /// <summary> 数据库访问 </summary>
        private readonly SQLHelper _sql;
        /// <summary> 电机码配置缓存 </summary>
        private readonly IMotorCodeCacheService _motorCodeCache;
        /// <summary> 日志 </summary>
        private readonly ILogger _logger;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入 SQL、电机码缓存与日志 </summary>
        public MotorCodeDispatchService(
            SQLHelper sql,
            IMotorCodeCacheService motorCodeCache,
            ILogger logger)
        {
            _sql = sql;
            _motorCodeCache = motorCodeCache;
            _logger = logger;
        }

        #endregion

        #region ===================== 900 下发 =====================

        /// <summary>
        /// 按流水码与型号 Id 获取下发数据：最新绑定电机码 + 平台代号。
        /// </summary>
        public async Task<MotorCodeDispatchResult> GetDispatchDataAsync(
            string flowCode,
            int productTypeId,
            string? motorMaterialName = null)
        {
            if (string.IsNullOrWhiteSpace(flowCode))
                return MotorCodeDispatchResult.Fail("流水码为空"); // 前置校验

            if (productTypeId <= 0)
                return MotorCodeDispatchResult.Fail("产品型号未下发或无效"); // 型号必须有效

            var materialName = string.IsNullOrWhiteSpace(motorMaterialName)
                ? DefaultMotorMaterialName // 默认物料名「电机码」
                : motorMaterialName.Trim();

            const string motorSql = @"
SELECT TOP 1 BindMaterialCode
FROM report_MaterialBind
WHERE FlowCode = @FlowCode
  AND BindMaterialName = @MaterialName
  AND BindStatus = @BoundStatus
ORDER BY BindTime DESC";

            var motorCode = await _sql.QuerySingleAsync<string?>(motorSql, new
            {
                FlowCode = flowCode.Trim(),
                MaterialName = materialName,
                BoundStatus = BindStatusConstants.Bound // 仅查当前绑定状态
            });

            if (string.IsNullOrWhiteSpace(motorCode))
                return MotorCodeDispatchResult.Fail($"流水码 {flowCode} 未绑定电机码({materialName})");

            var snapshot = _motorCodeCache.GetSnapshot(); // 从缓存取固定片段配置
            var platform = snapshot.FixedSegments
                .Where(f => f.ProductTypeId == productTypeId
                            && f.UsageType == (int)MotorCodeFixedSegmentUsage.Platform) // 平台代号用途
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Id)
                .FirstOrDefault();

            if (platform == null || string.IsNullOrWhiteSpace(platform.FixedValue))
                return MotorCodeDispatchResult.Fail("该型号未配置平台代号固定信息");

            _logger.Info(
                $"900 下发数据 FlowCode={flowCode}, TypeId={productTypeId}, Motor={motorCode}, Platform={platform.FixedValue}",
                LogSource);

            return MotorCodeDispatchResult.Ok(motorCode.Trim(), platform.FixedValue.Trim());
        }

        #endregion
    }
}
