namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 电机码 - 按型号日期代号对照 </summary>
    public class craft_MotorCodeDateMap : BaseEntity
    {
        #region ===================== 对照配置 =====================

        /// <summary> 产品型号 ID </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 对照类型：1 年 / 2 月 / 3 日 </summary>
        public int MapType { get; set; }

        /// <summary> 对照键（年/月/日的数值） </summary>
        public int MapKey { get; set; }

        /// <summary> 对照代号（映射后的字符） </summary>
        public string MapCode { get; set; } = string.Empty;

        #endregion
    }
}
