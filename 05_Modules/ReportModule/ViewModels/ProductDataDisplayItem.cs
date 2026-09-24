namespace ReportModule.ViewModels
{
    /// <summary>
    /// 产品加工数据查询结果行（report_ProcessHistory 展示绑定）。
    /// </summary>
    public class ProductDataDisplayItem
    {
        #region ===================== 基础字段 =====================

        /// <summary> 加工历史记录主键 </summary>
        public int Id { get; set; }

        /// <summary> 关联工位状态记录 Id </summary>
        public int StationRecordId { get; set; }

        /// <summary> 产品流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        #endregion

        #region ===================== 展示名称 =====================

        /// <summary> 工位名称 </summary>
        public string StationName { get; set; } = string.Empty;

        /// <summary> 产品型号名称 </summary>
        public string ProductTypeName { get; set; } = string.Empty;

        /// <summary> 产线名称 </summary>
        public string LineName { get; set; } = string.Empty;

        #endregion

        #region ===================== 采集数据 =====================

        /// <summary> 数据项名称 </summary>
        public string DataName { get; set; } = string.Empty;

        /// <summary> 采集值 </summary>
        public string DataValue { get; set; } = string.Empty;

        /// <summary> 数据类型 </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary> 数据单位 </summary>
        public string DataUnit { get; set; } = string.Empty;

        /// <summary> 是否返修（是/否） </summary>
        public string IsRepairText { get; set; } = string.Empty;

        /// <summary> 采集时间 </summary>
        public DateTime? CreateTime { get; set; }

        #endregion
    }
}
