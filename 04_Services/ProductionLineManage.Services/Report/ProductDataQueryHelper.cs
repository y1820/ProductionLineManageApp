using ProductionLineManage.Core.Models.DataBase;

namespace ProductionLineManage.Services.Report
{
    /// <summary> 产品加工数据查询上下文：封装筛选条件与 SQL 参数 </summary>
    public sealed class ProductDataQueryContext
    {
        #region --------------------- 筛选条件 ---------------------

        /// <summary> 流水码模糊匹配 </summary>
        public string FlowCode { get; init; } = string.Empty;
        /// <summary> 产线 Id，0 表示不限 </summary>
        public int LineId { get; init; }
        /// <summary> 工位 Id，0 表示不限 </summary>
        public int StationId { get; init; }
        /// <summary> 产品型号 Id，0 表示不限 </summary>
        public int ProductTypeId { get; init; }
        /// <summary> 采集时间起始 </summary>
        public DateTime StartTime { get; init; }
        /// <summary> 采集时间截止 </summary>
        public DateTime EndTime { get; init; }
        /// <summary> 产线下所有工位 Id </summary>
        public IReadOnlyList<int> LineStationIds { get; init; } = Array.Empty<int>();
        /// <summary> 是否启用产线工位 IN 过滤 </summary>
        public bool UseLineStationFilter { get; init; }

        #endregion

        #region --------------------- 派生属性 ---------------------

        /// <summary> 选了产线但未选工位，且该产线下没有任何工位 </summary>
        public bool IsEmptyLineScope =>
            LineId > 0 && StationId == 0 && UseLineStationFilter && LineStationIds.Count == 0;

        #endregion

        #region --------------------- SQL 参数 ---------------------

        /// <summary> 转为分页查询 SQL 参数 </summary>
        public object ToSqlParameters(int offset = 0, int fetch = 0)
        {
            return new
            {
                FlowCode,
                LineId,
                StationId,
                ProductTypeId,
                StartTime,
                EndTime,
                LineStationIds = LineStationIds.Count > 0 ? LineStationIds : new List<int> { -1 },
                UseLineStationFilter = UseLineStationFilter ? 1 : 0,
                Offset = offset,
                Fetch = fetch
            };
        }

        /// <summary> 转为导出批量查询 SQL 参数（支持 Keyset 分页） </summary>
        public object ToExportSqlParameters(DateTime? lastCreateTime, int lastId, int fetch)
        {
            return new
            {
                FlowCode,
                LineId,
                StationId,
                ProductTypeId,
                StartTime,
                EndTime,
                LineStationIds = LineStationIds.Count > 0 ? LineStationIds : new List<int> { -1 },
                UseLineStationFilter = UseLineStationFilter ? 1 : 0,
                UseKeyset = lastId > 0 ? 1 : 0,
                LastCreateTime = lastCreateTime ?? DateTime.MaxValue,
                LastId = lastId,
                Fetch = fetch
            };
        }

        #endregion
    }

    /// <summary>
    /// 产品加工数据查询辅助：WHERE 片段、分页/计数/导出 SQL 及上下文构建。
    /// </summary>
    public static class ProductDataQueryHelper
    {
        #region ===================== SQL 片段 =====================

        /// <summary> 通用 WHERE 条件 </summary>
        public const string WhereClause = @"
            (@FlowCode = '' OR FlowCode LIKE '%' + @FlowCode + '%')
            AND (@StationId = 0 OR StationId = @StationId)
            AND (@ProductTypeId = 0 OR ProductTypeId = @ProductTypeId)
            AND (@LineId = 0 OR @StationId > 0 OR LineId = @LineId OR (@UseLineStationFilter = 1 AND StationId IN @LineStationIds))
            AND CreateTime >= @StartTime AND CreateTime <= @EndTime";

        /// <summary> 计数 SQL </summary>
        public const string CountSql = $@"
            SELECT COUNT(1)
            FROM report_ProcessHistory
            WHERE {WhereClause}";

        /// <summary> 分页查询 SQL </summary>
        public const string PageSql = $@"
            SELECT *
            FROM report_ProcessHistory
            WHERE {WhereClause}
            ORDER BY CreateTime DESC, Id DESC
            OFFSET @Offset ROWS FETCH NEXT @Fetch ROWS ONLY";

        /// <summary> 导出批量 SQL </summary>
        public const string ExportBatchSql = $@"
            SELECT *
            FROM report_ProcessHistory
            WHERE {WhereClause}
              AND (@UseKeyset = 0 OR CreateTime < @LastCreateTime OR (CreateTime = @LastCreateTime AND Id < @LastId))
            ORDER BY CreateTime DESC, Id DESC
            OFFSET 0 ROWS FETCH NEXT @Fetch ROWS ONLY";

        #endregion

        #region ===================== 上下文构建 =====================

        /// <summary> 根据 UI 筛选条件构建查询上下文 </summary>
        public static ProductDataQueryContext Build(
            string? flowCode,
            int lineId,
            int stationId,
            int productTypeId,
            DateTime startTime,
            DateTime endTime,
            IEnumerable<craft_StationInfo> allStations)
        {
            //产线Id大于0 和 工位Id 等于 0 时 所有工位信息筛选(所属产线Id等于外部选择的产线Id)的结果里的每个集合Id输出一个新的集合对象
            //当条件不满足则new空集合
            //产线下所有工位Id
            var lineStationIds = lineId > 0 && stationId == 0
                ? allStations.Where(s => s.LineId == lineId).Select(s => s.Id).ToList()
                : new List<int>();

            return new ProductDataQueryContext
            {
                FlowCode = flowCode?.Trim() ?? string.Empty,//流水码
                LineId = lineId,//选择的产线Id
                StationId = stationId,//选择的工位Id
                ProductTypeId = productTypeId,//选择的型号Id
                StartTime = startTime,//开始时间
                EndTime = endTime,//结束时间
                LineStationIds = lineStationIds,//产线下所有工位Id
                UseLineStationFilter = lineId > 0 && stationId == 0 && lineStationIds.Count > 0 //产线下是否存在有效工位
            };
        }

        #endregion
    }
}
