namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 报表 - 工艺历史数据 </summary>
    public class report_ProcessHistory : BaseEntity
    {
        #region ===================== 产品标识 =====================

        /// <summary> 关联 production_ProductStationStatus.Id（工位状态，供跨项目判定合格与历史数据） </summary>
        public int StationRecordId { get; set; }

        /// <summary> 产品流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        /// <summary> 托盘号 </summary>
        public string TrayCode { get; set; } = string.Empty;

        /// <summary> 产品型号 ID </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 ID </summary>
        public int LineId { get; set; }

        /// <summary> 工位 Id </summary>
        public int StationId { get; set; }

        #endregion

        #region ===================== 采集数据 =====================

        /// <summary> 数据名称 </summary>
        public string DataName { get; set; } = string.Empty;

        /// <summary> 数据值 </summary>
        public string DataValue { get; set; } = string.Empty;

        /// <summary> 数据类型 </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary> 数据单位 </summary>
        public string DataUnit { get; set; } = string.Empty;

        #endregion

        #region ===================== 生产上下文 =====================

        /// <summary> 工单号 </summary>
        public string WorkOrderId { get; set; } = string.Empty;

        /// <summary> 操作工 ID </summary>
        public int OperatorId { get; set; }

        /// <summary> 班次 </summary>
        public string Shift { get; set; } = string.Empty;

        #endregion

        #region ===================== 质量与返修 =====================

        /// <summary> 是否合格 </summary>
        public bool IsQualified { get; set; }

        /// <summary> 不良原因 </summary>
        public string DefectiveReason { get; set; } = string.Empty;

        /// <summary> 是否为返修 </summary>
        public bool IsRepair { get; set; }

        /// <summary> 返修次数 </summary>
        public int RepairCount { get; set; }

        /// <summary> 返修原因 </summary>
        public string RepairReason { get; set; } = string.Empty;

        #endregion
    }
}
