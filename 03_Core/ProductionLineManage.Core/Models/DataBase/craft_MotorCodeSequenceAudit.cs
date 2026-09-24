namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 电机码 - 序列号手动调整审计 </summary>
    public class craft_MotorCodeSequenceAudit : EntityBase
    {
        #region ===================== 调整记录 =====================

        /// <summary> 产品型号 ID </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 调整前序号 </summary>
        public int OldValue { get; set; }

        /// <summary> 调整后序号 </summary>
        public int NewValue { get; set; }

        /// <summary> 调整前桶键（复位周期标识） </summary>
        public string OldBucketKey { get; set; } = string.Empty;

        /// <summary> 调整后桶键（复位周期标识） </summary>
        public string NewBucketKey { get; set; } = string.Empty;

        /// <summary> 操作人 </summary>
        public string Operator { get; set; } = string.Empty;

        /// <summary> 调整原因 </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary> 操作时间 </summary>
        public DateTime OperateTime { get; set; }

        #endregion
    }
}
