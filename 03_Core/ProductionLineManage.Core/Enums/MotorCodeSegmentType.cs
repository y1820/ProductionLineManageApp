namespace ProductionLineManage.Core.Enums
{
    /// <summary>电机码片段类型（与生成规则中的 D 槽位绑定）</summary>
    public enum MotorCodeSegmentType
    {
        /// <summary>年份片段</summary>
        Year = 1,

        /// <summary>月份片段</summary>
        Month = 2,

        /// <summary>日期片段</summary>
        Day = 3,

        /// <summary>序列号片段</summary>
        Sequence = 4,

        /// <summary>固定信息片段</summary>
        Fixed = 5
    }
}
