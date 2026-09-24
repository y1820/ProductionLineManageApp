namespace ReportModule.ViewModels
{
    /// <summary>
    /// 物料绑定状态筛选下拉项：供报表查询条件 ComboBox 绑定使用。
    /// </summary>
    public sealed class BindStatusFilterOption
    {
        #region ===================== 属性 =====================

        /// <summary> 绑定状态值（-1 表示全部，不筛选） </summary>
        public int Value { get; init; }

        /// <summary> 界面显示文本（如「全部」「已绑定」「未绑定」） </summary>
        public string Display { get; init; } = string.Empty;

        #endregion
    }
}
