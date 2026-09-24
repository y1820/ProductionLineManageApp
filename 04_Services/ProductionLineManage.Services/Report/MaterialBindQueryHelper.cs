using ProductionLineManage.Core.Constants;
using ProductionLineManage.Core.Models.DataBase;

namespace ProductionLineManage.Services.Report
{
    /// <summary> 物料绑定查询上下文：封装筛选条件与 SQL 参数 </summary>
    public sealed class MaterialBindQueryContext
    {
        #region --------------------- 筛选条件 ---------------------

        /// <summary> 产品型号 Id，0 表示不限 </summary>
        public int ProductTypeId { get; init; }
        /// <summary> 产线 Id，0 表示不限 </summary>
        public int LineId { get; init; }
        /// <summary> 工位 Id，0 表示不限 </summary>
        public int StationId { get; init; }
        /// <summary> 流水码模糊匹配 </summary>
        public string FlowCode { get; init; } = string.Empty;
        /// <summary> 绑定物料码模糊匹配 </summary>
        public string BindMaterialCode { get; init; } = string.Empty;
        /// <summary> 绑定状态，-1 表示不限 </summary>
        public int BindStatus { get; init; } = -1;
        /// <summary> 产线下所有工位 Id（产线级筛选时使用） </summary>
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
        public object ToSqlParameters(int offset = 0, int fetch = 0) => new
        {
            ProductTypeId,
            LineId,
            StationId,
            FlowCode,
            BindMaterialCode,
            BindStatus,
            LineStationIds = LineStationIds.Count > 0 ? LineStationIds : new List<int> { -1 }, // 空列表占位
            UseLineStationFilter = UseLineStationFilter ? 1 : 0,
            Offset = offset,
            Fetch = fetch
        };

        /// <summary> 转为导出批量查询 SQL 参数（支持 Keyset 分页） </summary>
        public object ToExportSqlParameters(DateTime? lastBindTime, int lastId, int fetch) => new
        {
            ProductTypeId,
            LineId,
            StationId,
            FlowCode,
            BindMaterialCode,
            BindStatus,
            LineStationIds = LineStationIds.Count > 0 ? LineStationIds : new List<int> { -1 },
            UseLineStationFilter = UseLineStationFilter ? 1 : 0,
            UseKeyset = lastId > 0 ? 1 : 0, // 是否启用游标分页
            LastBindTime = lastBindTime ?? DateTime.MaxValue,
            LastId = lastId,
            Fetch = fetch
        };

        #endregion
    }

    /// <summary>
    /// 物料绑定查询辅助：WHERE 片段、分页/计数/导出 SQL 及上下文构建。
    /// </summary>
    public static class MaterialBindQueryHelper
    {
        #region ===================== SQL 片段 =====================

        /// <summary> 通用 WHERE 条件（型号/工位/产线/流水码/物料码/绑定状态） </summary>
        public const string WhereClause = @"
            (@ProductTypeId = 0 OR ProductTypeId = @ProductTypeId)
            AND (@StationId = 0 OR StationId = @StationId)
            AND (@LineId = 0 OR @StationId > 0 OR LineId = @LineId OR (@UseLineStationFilter = 1 AND StationId IN @LineStationIds))
            AND (@FlowCode = '' OR FlowCode LIKE '%' + @FlowCode + '%')
            AND (@BindMaterialCode = '' OR BindMaterialCode LIKE '%' + @BindMaterialCode + '%')
            AND (@BindStatus < 0 OR BindStatus = @BindStatus)";

        /// <summary> 计数 SQL </summary>
        public const string CountSql = $@"
            SELECT COUNT(1)
            FROM report_MaterialBind
            WHERE {WhereClause}";

        /// <summary> 分页查询 SQL </summary>
        public const string PageSql = $@"
            SELECT *
            FROM report_MaterialBind
            WHERE {WhereClause}
            ORDER BY BindTime DESC, Id DESC
            OFFSET @Offset ROWS FETCH NEXT @Fetch ROWS ONLY";

        /// <summary> 导出批量 SQL（Keyset 游标避免大 OFFSET） </summary>
        public const string ExportBatchSql = $@"
            SELECT *
            FROM report_MaterialBind
            WHERE {WhereClause}
              AND (@UseKeyset = 0 OR BindTime < @LastBindTime OR (BindTime = @LastBindTime AND Id < @LastId))
            ORDER BY BindTime DESC, Id DESC
            OFFSET 0 ROWS FETCH NEXT @Fetch ROWS ONLY";

        #endregion

        #region ===================== 上下文构建 =====================

        /// <summary> 根据 UI 筛选条件构建查询上下文 </summary>
        public static MaterialBindQueryContext Build(
            int productTypeId,
            int lineId,
            int stationId,
            string? flowCode,
            string? bindMaterialCode,
            int bindStatus,
            IEnumerable<craft_StationInfo> allStations)
        {
            var lineStationIds = lineId > 0 && stationId == 0
                ? allStations.Where(s => s.LineId == lineId).Select(s => s.Id).ToList() // 产线级：收集下属工位
                : new List<int>();

            return new MaterialBindQueryContext
            {
                ProductTypeId = productTypeId,
                LineId = lineId,
                StationId = stationId,
                FlowCode = flowCode?.Trim() ?? string.Empty,
                BindMaterialCode = bindMaterialCode?.Trim() ?? string.Empty,
                BindStatus = bindStatus,
                LineStationIds = lineStationIds,
                UseLineStationFilter = lineId > 0 && stationId == 0 && lineStationIds.Count > 0
            };
        }

        /// <summary> 绑定状态枚举转中文显示 </summary>
        public static string GetBindStatusText(int status) => status switch
        {
            BindStatusConstants.Unbound => "未绑定",
            BindStatusConstants.Bound => "绑定",
            BindStatusConstants.UnboundHistory => "解绑",
            BindStatusConstants.Scrapped => "报废",
            _ => status.ToString()
        };

        #endregion
    }
}
