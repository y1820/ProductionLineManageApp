namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 报表 - 过站明细（可重复，记录每次 200~8000 的过站周期） </summary>
    public class report_StationPassRecord : BaseEntity
    {
        #region ===================== 产品标识 =====================

        /// <summary> 产品流水码 </summary>
        public string FlowCode { get; set; } = string.Empty;

        /// <summary> 托盘号 </summary>
        public string TrayCode { get; set; } = string.Empty;

        /// <summary> 产品型号 ID </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 ID </summary>
        public int LineId { get; set; }

        /// <summary> 工位 ID </summary>
        public int StationId { get; set; }

        #endregion

        #region ===================== 过站时间 =====================

        /// <summary> 进站时间（200） </summary>
        public DateTime StartTime { get; set; }

        /// <summary> 出站时间（8000），未完成时为 null </summary>
        public DateTime? EndTime { get; set; }

        #endregion
    }
}
