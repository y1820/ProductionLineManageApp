namespace ProductionLineManage.Core.Enums
{
    /// <summary>电机码序列号复位周期</summary>
    public enum MotorCodeResetCycle
    {
        /// <summary>每日复位</summary>
        Daily = 1,

        /// <summary>每周复位</summary>
        Weekly = 2,

        /// <summary>每月复位</summary>
        Monthly = 3,

        /// <summary>每年复位</summary>
        Yearly = 4,

        /// <summary>不复位</summary>
        Never = 5
    }
}
