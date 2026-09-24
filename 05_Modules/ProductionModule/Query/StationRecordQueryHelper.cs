namespace ProductionModule.Query
{
    /// <summary>
    /// 过站记录查询上下文：封装筛选条件并动态构建 WHERE 子句与参数。
    /// </summary>
    internal sealed class StationRecordQueryContext
    {
        #region ===================== 查询条件 =====================

        /// <summary> 产品流水号（模糊匹配） </summary>
        public string FlowCode { get; init; } = string.Empty;

        /// <summary> 产线 ID（0 或负数表示不限） </summary>
        public int LineId { get; init; }

        /// <summary> 工站 ID（0 或负数表示不限） </summary>
        public int StationId { get; init; }

        /// <summary> 产品型号 ID（0 或负数表示不限） </summary>
        public int ProductTypeId { get; init; }

        /// <summary> 过站状态（-1 表示不限；0 待过站 / 1 合格 / 2 不合格） </summary>
        public int Status { get; init; } = -1;

        /// <summary> 是否返修（-1 表示不限；0 否 / 1 是） </summary>
        public int IsRepair { get; init; } = -1;

        /// <summary> 查询起始时间（含） </summary>
        public DateTime QueryStartTime { get; init; }

        /// <summary> 查询结束时间（含） </summary>
        public DateTime QueryEndTime { get; init; }

        #endregion

        #region ===================== 公共方法 =====================

        /// <summary> 根据当前条件构建 WHERE 子句与 Dapper 参数字典 </summary>
        public (string WhereSql, Dictionary<string, object?> Parameters) BuildWhere()
        {
            var clauses = new List<string> { "UpdateTime >= @StartTime", "UpdateTime <= @EndTime" }; // 时间范围必选
            var param = new Dictionary<string, object?>
            {
                ["StartTime"] = QueryStartTime,
                ["EndTime"] = QueryEndTime
            };

            if (!string.IsNullOrWhiteSpace(FlowCode)) // 流水号模糊匹配
            {
                clauses.Add("FlowCode LIKE @FlowCode");
                param["FlowCode"] = $"%{FlowCode.Trim()}%";
            }

            if (LineId > 0) // 产线筛选
            {
                clauses.Add("LineId = @LineId");
                param["LineId"] = LineId;
            }

            if (StationId > 0) // 工站筛选
            {
                clauses.Add("StationId = @StationId");
                param["StationId"] = StationId;
            }

            if (ProductTypeId > 0) // 产品型号筛选
            {
                clauses.Add("ProductTypeId = @ProductTypeId");
                param["ProductTypeId"] = ProductTypeId;
            }

            if (Status >= 0) // 过站状态筛选
            {
                clauses.Add("Status = @Status");
                param["Status"] = Status;
            }

            if (IsRepair >= 0) // 返修标志筛选
            {
                clauses.Add("IsRepair = @IsRepair");
                param["IsRepair"] = IsRepair == 1; // 转为 bool 写入 SQL 参数
            }

            return ($"WHERE {string.Join(" AND ", clauses)}", param);
        }

        /// <summary> 构建 COUNT 查询所需的参数字典 </summary>
        public object ToCountParameters()
        {
            var (_, param) = BuildWhere(); // 复用 WHERE 条件参数
            return param;
        }

        /// <summary> 构建分页查询所需的参数字典（附加 Offset / PageSize） </summary>
        public object ToPageParameters(int offset, int pageSize)
        {
            var (_, param) = BuildWhere(); // 复用 WHERE 条件参数
            param["Offset"] = offset; // 分页偏移
            param["PageSize"] = pageSize; // 每页条数
            return param;
        }

        #endregion
    }

    /// <summary>
    /// 过站记录查询辅助类：提供 SQL 模板、WHERE 替换与状态文本格式化。
    /// </summary>
    internal static class StationRecordQueryHelper
    {
        #region ===================== SQL 模板 =====================

        /// <summary> 统计总条数的 SQL 模板（/**WHERE**/ 占位符由 ApplyWhere 替换） </summary>
        public const string CountSql = @"
            SELECT COUNT(1)
            FROM production_ProductStationStatus
            /**WHERE**/";

        /// <summary> 分页查询 SQL 模板（按 UpdateTime、Id 降序） </summary>
        public const string PageSql = @"
            SELECT *
            FROM production_ProductStationStatus
            /**WHERE**/
            ORDER BY UpdateTime DESC, Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        #endregion

        #region ===================== 公共方法 =====================

        /// <summary> 将 SQL 模板中的 /**WHERE**/ 替换为上下文构建的 WHERE 子句 </summary>
        public static string ApplyWhere(string sql, StationRecordQueryContext context)
        {
            var (where, _) = context.BuildWhere(); // 获取动态 WHERE
            return sql.Replace("/**WHERE**/", where); // 替换占位符
        }

        /// <summary> 将过站状态码转为界面显示文本 </summary>
        public static string FormatStatus(int status) => status switch
        {
            0 => "待过站",
            1 => "合格",
            2 => "不合格",
            _ => status.ToString() // 未知状态直接显示数字
        };

        #endregion
    }
}
