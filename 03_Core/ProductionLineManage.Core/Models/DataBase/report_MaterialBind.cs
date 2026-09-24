namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 报表 - 物料绑定记录 </summary>
    public class report_MaterialBind : BaseEntity
    {
        #region ===================== 关联标识 =====================

        /// <summary> 产品型号 Id </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 ID </summary>
        public int LineId { get; set; }

        /// <summary> 工位 Id </summary>
        public int StationId { get; set; }

        /// <summary> 主物料（流水码） </summary>
        public string FlowCode { get; set; } = string.Empty;

        #endregion

        #region ===================== 绑定信息 =====================

        /// <summary> 绑定物料名称 </summary>
        public string BindMaterialName { get; set; } = string.Empty;

        /// <summary> 绑定物料码 </summary>
        public string BindMaterialCode { get; set; } = string.Empty;

        /// <summary> 绑定状态（参见 BindStatusConstants） </summary>
        public int BindStatus { get; set; }

        /// <summary> 绑定时间 </summary>
        public DateTime BindTime { get; set; }

        /// <summary> 解绑时间 </summary>
        public DateTime UnbindTime { get; set; }

        #endregion
    }
}
