namespace ReportModule.ViewModels
{
    /// <summary>
    /// 工位过站记录查询结果行（过站起止时间与状态展示）。
    /// </summary>
    public class StationPassRecordDisplayItem
    {
        #region ===================== 基础字段 =====================

        /// <summary> 过站记录主键 </summary>
        public int Id { get; set; }

        /// <summary> 产品流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        /// <summary> 托盘码 </summary>
        public string TrayCode { get; set; } = string.Empty;

        #endregion

        #region ===================== 展示名称 =====================

        /// <summary> 工位名称 </summary>
        public string StationName { get; set; } = string.Empty;

        /// <summary> 产品型号名称 </summary>
        public string ProductTypeName { get; set; } = string.Empty;

        /// <summary> 产线名称 </summary>
        public string LineName { get; set; } = string.Empty;

        #endregion

        #region ===================== 过站信息 =====================

        /// <summary> 进站时间 </summary>
        public DateTime? StartTime { get; set; }

        /// <summary> 出站时间 </summary>
        public DateTime? EndTime { get; set; }

        /// <summary> 过站状态文本 </summary>
        public string PassStateText { get; set; } = string.Empty;

        #endregion
    }
}
