namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 电机码 - 按型号固定信息片段 </summary>
    public class craft_MotorCodeFixedSegment : BaseEntity
    {
        #region ===================== 片段配置 =====================

        /// <summary> 产品型号 ID </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 片段代号（如 D1、D2） </summary>
        public string SegmentCode { get; set; } = string.Empty;

        /// <summary> 固定值内容 </summary>
        public string FixedValue { get; set; } = string.Empty;

        /// <summary> 排序序号 </summary>
        public int SortOrder { get; set; }

        /// <summary> 用途类型：0 普通 / 1 平台代号（每型号最多一条） </summary>
        public int UsageType { get; set; }

        #endregion
    }
}
