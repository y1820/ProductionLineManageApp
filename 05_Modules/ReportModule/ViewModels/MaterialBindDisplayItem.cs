namespace ReportModule.ViewModels
{
    /// <summary>
    /// 物料绑定记录查询结果行（绑定/解绑历史展示）。
    /// </summary>
    public class MaterialBindDisplayItem
    {
        #region ===================== 基础字段 =====================

        /// <summary> 绑定记录主键 </summary>
        public int Id { get; set; }

        /// <summary> 产品流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        #endregion

        #region ===================== 展示名称 =====================

        /// <summary> 产品型号名称 </summary>
        public string ProductTypeName { get; set; } = string.Empty;

        /// <summary> 产线名称 </summary>
        public string LineName { get; set; } = string.Empty;

        /// <summary> 工位名称 </summary>
        public string StationName { get; set; } = string.Empty;

        #endregion

        #region ===================== 绑定信息 =====================

        /// <summary> 绑定物料名称 </summary>
        public string BindMaterialName { get; set; } = string.Empty;

        /// <summary> 绑定物料编码 </summary>
        public string BindMaterialCode { get; set; } = string.Empty;

        /// <summary> 绑定状态文本 </summary>
        public string BindStatusText { get; set; } = string.Empty;

        /// <summary> 绑定时间 </summary>
        public DateTime BindTime { get; set; }

        /// <summary> 解绑时间（未解绑时为 null） </summary>
        public DateTime? UnbindTime { get; set; }

        #endregion
    }
}
