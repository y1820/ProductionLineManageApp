namespace ProductionModule.ViewModels
{
    /// <summary>
    /// 产品工位状态查询展示行（production_ProductStationStatus 查询结果绑定）。
    /// </summary>
    public class StationRecordDisplayItem
    {
        #region ===================== 基础字段 =====================

        /// <summary> 记录主键 </summary>
        public int Id { get; set; }

        /// <summary> 产品流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        /// <summary> 托盘码 </summary>
        public string TrayCode { get; set; } = string.Empty;

        /// <summary> 工位 Id </summary>
        public int StationId { get; set; }

        /// <summary> 产品型号 Id </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 Id </summary>
        public int LineId { get; set; }

        #endregion

        #region ===================== 展示名称 =====================

        /// <summary> 工位名称 </summary>
        public string StationName { get; set; } = string.Empty;

        /// <summary> 产品型号名称 </summary>
        public string ProductTypeName { get; set; } = string.Empty;

        /// <summary> 产线名称 </summary>
        public string LineName { get; set; } = string.Empty;

        #endregion

        #region ===================== 状态与返修 =====================

        /// <summary> 过站状态码（0 待过站 / 1 合格 / 2 不合格） </summary>
        public int Status { get; set; }

        /// <summary> 过站状态文本 </summary>
        public string StatusText { get; set; } = string.Empty;

        /// <summary> 是否返修（是/否） </summary>
        public string IsRepairText { get; set; } = string.Empty;

        /// <summary> 返修目标工位名称 </summary>
        public string RepairTargetStationName { get; set; } = string.Empty;

        /// <summary> 返修次数 </summary>
        public int RepairCount { get; set; }

        #endregion

        #region ===================== 时间 =====================

        /// <summary> 开始时间 </summary>
        public DateTime? StartTime { get; set; }

        /// <summary> 结束时间 </summary>
        public DateTime? EndTime { get; set; }

        /// <summary> 最后更新时间 </summary>
        public DateTime? UpdateTime { get; set; }

        /// <summary> 创建时间 </summary>
        public DateTime? CreateTime { get; set; }

        #endregion
    }

    /// <summary> 过站状态筛选下拉项 </summary>
    public sealed class StatusFilterOption
    {
        /// <summary> 状态值（-1 表示全部） </summary>
        public int Value { get; init; }

        /// <summary> 界面显示文本 </summary>
        public string Display { get; init; } = string.Empty;
    }

    /// <summary> 返修筛选下拉项 </summary>
    public sealed class RepairFilterOption
    {
        /// <summary> 返修标志值（-1 表示全部） </summary>
        public int Value { get; init; }

        /// <summary> 界面显示文本 </summary>
        public string Display { get; init; } = string.Empty;
    }
}
