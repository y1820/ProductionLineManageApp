namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 工艺 - 产线基础信息 </summary>
    public class craft_LineInfo : BaseEntity
    {
        #region ===================== 基础属性 =====================

        /// <summary> 产线名称 </summary>
        public string Name { get; set; } = string.Empty;

        #endregion
    }
}
