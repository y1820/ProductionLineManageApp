namespace ReportModule.Query
{
    /// <summary>
    /// 工位过站记录查询上下文：封装筛选条件并生成 SQL WHERE 子句与 Dapper 参数。
    /// </summary>
    internal sealed class StationPassRecordQueryContext
    {
        #region ===================== 筛选条件 =====================

        /// <summary> 流水码（模糊匹配，空则不过滤） </summary>
        public string FlowCode { get; init; } = string.Empty;

        /// <summary> 产线 Id（0 表示全部） </summary>
        public int LineId { get; init; }

        /// <summary> 工位 Id（0 表示全部） </summary>
        public int StationId { get; init; }

        /// <summary> 产品型号 Id（0 表示全部） </summary>
        public int ProductTypeId { get; init; }

        /// <summary> 查询起始时间 </summary>
        public DateTime QueryStartTime { get; init; }

        /// <summary> 查询结束时间 </summary>
        public DateTime QueryEndTime { get; init; }

        #endregion

        #region ===================== 参数构建 =====================

        /// <summary> 构建 Dapper 参数字典（含时间与可选筛选字段） </summary>
        public Dictionary<string, object?> BuildParameters()
        {
            var param = new Dictionary<string, object?>
            {
                ["StartTime"] = QueryStartTime,
                ["EndTime"] = QueryEndTime
            };

            if (!string.IsNullOrWhiteSpace(FlowCode))
            {
                param["FlowCode"] = $"%{FlowCode.Trim()}%"; // 模糊匹配流水码
            }

            if (LineId > 0) param["LineId"] = LineId;
            if (StationId > 0) param["StationId"] = StationId;
            if (ProductTypeId > 0) param["ProductTypeId"] = ProductTypeId;

            return param;
        }

        /// <summary> 根据当前筛选条件生成 WHERE 子句 </summary>
        public string BuildWhere()
        {
            var clauses = new List<string> { "StartTime >= @StartTime", "StartTime <= @EndTime" }; // 时间范围必选

            if (!string.IsNullOrWhiteSpace(FlowCode))
                clauses.Add("FlowCode LIKE @FlowCode");
            if (LineId > 0)
                clauses.Add("LineId = @LineId");
            if (StationId > 0)
                clauses.Add("StationId = @StationId");
            if (ProductTypeId > 0)
                clauses.Add("ProductTypeId = @ProductTypeId");

            return $"WHERE {string.Join(" AND ", clauses)}";
        }

        /// <summary> 构建分页查询参数（在 BuildParameters 基础上追加 Offset/PageSize） </summary>
        public object ToPageParameters(int offset, int pageSize)
        {
            var dict = BuildParameters();
            dict["Offset"] = offset;
            dict["PageSize"] = pageSize;
            return dict;
        }

        #endregion
    }

    /// <summary>
    /// 工位过站记录 SQL 模板与 WHERE 替换辅助类。
    /// </summary>
    internal static class StationPassRecordQueryHelper
    {
        #region ===================== SQL 模板 =====================

        /// <summary> 统计总记录数 SQL（/**WHERE**/ 占位符由 ApplyWhere 替换） </summary>
        public const string CountSql = @"
            SELECT COUNT(1)
            FROM report_StationPassRecord
            /**WHERE**/";

        /// <summary> 分页查询 SQL（按进站时间倒序） </summary>
        public const string PageSql = @"
            SELECT *
            FROM report_StationPassRecord
            /**WHERE**/
            ORDER BY StartTime DESC, Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        #endregion

        #region ===================== 辅助方法 =====================

        /// <summary> 将 /**WHERE**/ 占位符替换为实际上下文 WHERE 子句 </summary>
        public static string ApplyWhere(string sql, StationPassRecordQueryContext context) =>
            sql.Replace("/**WHERE**/", context.BuildWhere());

        #endregion
    }
}
