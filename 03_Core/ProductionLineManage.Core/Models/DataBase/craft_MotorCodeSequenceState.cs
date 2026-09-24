namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 电机码 - 按型号序列号运行时状态 </summary>
    public class craft_MotorCodeSequenceState : EntityBase
    {
        #region ===================== 运行时状态 =====================

        /// <summary> 产品型号 ID </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 桶键（复位周期标识，默认 GLOBAL） </summary>
        public string BucketKey { get; set; } = "GLOBAL";

        /// <summary> 当前已发出的最大序号 </summary>
        public int CurrentValue { get; set; }

        /// <summary> 上次复位时间 </summary>
        public DateTime? LastResetTime { get; set; }

        /// <summary> 最后更新时间 </summary>
        public DateTime? UpdateTime { get; set; }

        #endregion
    }
}
