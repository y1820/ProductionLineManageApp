namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 电机码 - 按型号片段绑定 </summary>
    public class craft_MotorCodeSegmentBind : BaseEntity
    {
        #region ===================== 绑定配置 =====================

        /// <summary> 产品型号 ID </summary>
        public int ProductTypeId { get; set; }

        /// <summary> 片段代号（如 D1、D2） </summary>
        public string SegmentCode { get; set; } = string.Empty;

        /// <summary> 片段类型：1 年 / 2 月 / 3 日 / 4 序列 / 5 固定 </summary>
        public int SegmentType { get; set; }

        /// <summary> 固定片段 ID，关联 craft_MotorCodeFixedSegment.Id；0 表示未选 </summary>
        public int FixedSegmentId { get; set; }

        /// <summary> 排序序号 </summary>
        public int SortOrder { get; set; }

        #endregion
    }
}
