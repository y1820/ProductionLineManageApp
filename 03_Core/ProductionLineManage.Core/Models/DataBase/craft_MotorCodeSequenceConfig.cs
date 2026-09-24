namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 电机码 - 按型号序列号配置（每型号独立位数/步长/复位周期） </summary>
    public class craft_MotorCodeSequenceConfig : BaseEntity
    {
        #region ===================== 序列配置 =====================

        /// <summary> 产品型号 ID </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 序号位数 </summary>
        public int DigitLength { get; set; } = 4;

        /// <summary> 起始值 </summary>
        public int StartValue { get; set; } = 1;

        /// <summary> 自增系数（每次取号 +Step） </summary>
        public int Step { get; set; } = 1;

        /// <summary> 复位周期（参见 MotorCodeResetCycle 枚举） </summary>
        public int ResetCycle { get; set; } = 1;

        /// <summary> 是否启用 </summary>
        public bool IsEnabled { get; set; } = true;

        #endregion
    }
}
