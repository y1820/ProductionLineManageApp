namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 工艺 - 工位流水码规则 </summary>
    public class craft_FlowCodeRules : BaseRulesItem
    {
        #region ===================== 关联标识 =====================

        /// <summary> 产品型号 Id </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 产线 ID </summary>
        public int LineId { get; set; }

        /// <summary> 工位 Id </summary>
        public int StationId { get; set; }

        #endregion
    }
}
