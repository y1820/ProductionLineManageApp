namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 生产 - 产品工位状态（型号+产线+工位+流水码唯一，供流程判断与员工维护） </summary>
    public class production_ProductStationStatus : BaseEntity
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

        #region ===================== 状态与返修 =====================

        /// <summary> 状态：0待过站 / 1合格 / 2不合格 </summary>
        public int Status { get; set; }

        /// <summary> 是否处于返修待加工 </summary>
        public bool IsRepair { get; set; }

        /// <summary> 返修目标工位 ID </summary>
        public int RepairTargetStationId { get; set; }

        /// <summary> 返修次数 </summary>
        public int RepairCount { get; set; }

        #endregion
    }
}
