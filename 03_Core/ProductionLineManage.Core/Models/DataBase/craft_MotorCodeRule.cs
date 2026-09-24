namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 电机码 - 按型号生成规则 </summary>
    public class craft_MotorCodeRule : BaseEntity
    {
        #region ===================== 规则配置 =====================

        /// <summary> 产品型号 ID </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 组合公式，如 D1+D2+D3+D4+D5+D6 </summary>
        public string Formula { get; set; } = string.Empty;

        /// <summary> 是否启用 </summary>
        public bool IsEnabled { get; set; } = true;

        #endregion
    }
}
