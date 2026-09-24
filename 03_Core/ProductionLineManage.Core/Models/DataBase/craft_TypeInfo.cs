namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 工艺 - 产品型号基础信息 </summary>
    public class craft_TypeInfo : BaseEntity
    {
        #region ===================== 型号属性 =====================

        /// <summary> 型号名称 </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary> 下发型号代号，与 PLC 内部型号对应（下发 short 值，非数据库 Id） </summary>
        public int IssueCode { get; set; }

        #endregion
    }
}
