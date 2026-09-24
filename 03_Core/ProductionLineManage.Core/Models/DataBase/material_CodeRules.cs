namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 物料编码规则数据实体 </summary>
    public class material_CodeRules : BaseRulesItem
    {
        #region ===================== 关联标识 =====================

        /// <summary> 型号 Id </summary>
        public int TypeId { get; set; }

        /// <summary> 物料 Id </summary>
        public int MaterialId { get; set; }

        #endregion
    }
}
