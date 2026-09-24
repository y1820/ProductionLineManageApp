namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 物料信息数据实体 </summary>
    public class material_Info : BaseEntity
    {
        #region ===================== 物料属性 =====================

        /// <summary> 物料代号 </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary> 物料名称 </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary> 型号 Id </summary>
        public int TypeId { get; set; }

        #endregion
    }
}
