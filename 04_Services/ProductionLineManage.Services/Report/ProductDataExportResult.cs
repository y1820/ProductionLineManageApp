namespace ProductionLineManage.Services.Report
{
    /// <summary>
    /// 产品/物料数据导出结果：输出文件路径列表与分页数。
    /// CSV 超行数时分多个文件，Excel 超行数时分多个 Sheet。
    /// </summary>
    public sealed class ProductDataExportResult
    {
        #region ===================== 结果属性 =====================

        /// <summary> 所有输出文件路径（CSV 分页时有多条） </summary>
        public required IReadOnlyList<string> OutputPaths { get; init; }

        /// <summary> 分页总数（Sheet 数或 CSV 文件数） </summary>
        public int PageCount { get; init; }

        /// <summary> 主输出路径（第一个文件，便于单文件场景直接使用） </summary>
        public string PrimaryPath => OutputPaths.Count > 0 ? OutputPaths[0] : string.Empty; // 无文件时返回空串

        #endregion
    }
}
