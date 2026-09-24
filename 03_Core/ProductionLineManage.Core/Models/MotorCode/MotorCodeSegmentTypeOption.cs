using ProductionLineManage.Core.Enums;

namespace ProductionLineManage.Core.Models.MotorCode
{
    #region ===================== 筛选分类枚举 =====================

    /// <summary> 生成规则页「信息类型」筛选分类 </summary>
    public enum MotorCodeSegmentFilterCategory
    {
        /// <summary> 全部 </summary>
        All = 0,

        /// <summary> 固定信息 </summary>
        Fixed = 1,

        /// <summary> 日期代号 </summary>
        Date = 2,

        /// <summary> 序列号 </summary>
        Sequence = 3
    }

    #endregion

    #region ===================== 片段类型 UI 选项 =====================

    /// <summary> 片段数据类型 UI 选项 </summary>
    public sealed class MotorCodeSegmentTypeOption
    {
        /// <summary> 枚举值 </summary>
        public int Value { get; init; }

        /// <summary> 显示文本 </summary>
        public string Display { get; init; } = string.Empty;

        /// <summary> 所属筛选分类 </summary>
        public MotorCodeSegmentFilterCategory Category { get; init; }

        /// <summary> 全部片段类型选项 </summary>
        public static IReadOnlyList<MotorCodeSegmentTypeOption> All { get; } =
        [
            new() { Value = (int)MotorCodeSegmentType.Year, Display = "年代号", Category = MotorCodeSegmentFilterCategory.Date },
            new() { Value = (int)MotorCodeSegmentType.Month, Display = "月代号", Category = MotorCodeSegmentFilterCategory.Date },
            new() { Value = (int)MotorCodeSegmentType.Day, Display = "日代号", Category = MotorCodeSegmentFilterCategory.Date },
            new() { Value = (int)MotorCodeSegmentType.Sequence, Display = "序列号", Category = MotorCodeSegmentFilterCategory.Sequence },
            new() { Value = (int)MotorCodeSegmentType.Fixed, Display = "固定信息", Category = MotorCodeSegmentFilterCategory.Fixed }
        ];

        /// <summary> 按片段类型值获取显示文本 </summary>
        public static string GetDisplay(int segmentType) =>
            All.FirstOrDefault(x => x.Value == segmentType)?.Display ?? segmentType.ToString();

        /// <summary> 按片段类型值获取筛选分类 </summary>
        public static MotorCodeSegmentFilterCategory GetCategory(int segmentType) =>
            All.FirstOrDefault(x => x.Value == segmentType)?.Category ?? MotorCodeSegmentFilterCategory.All;
    }

    #endregion

    #region ===================== 筛选分类 UI 选项 =====================

    /// <summary> 生成规则页「信息类型」筛选下拉选项 </summary>
    public sealed class MotorCodeFilterCategoryOption
    {
        /// <summary> 分类值 </summary>
        public int Value { get; init; }

        /// <summary> 显示文本 </summary>
        public string Display { get; init; } = string.Empty;

        /// <summary> 全部分类选项 </summary>
        public static IReadOnlyList<MotorCodeFilterCategoryOption> All { get; } =
        [
            new() { Value = (int)MotorCodeSegmentFilterCategory.All, Display = "全部" },
            new() { Value = (int)MotorCodeSegmentFilterCategory.Fixed, Display = "固定信息" },
            new() { Value = (int)MotorCodeSegmentFilterCategory.Date, Display = "日期代号" },
            new() { Value = (int)MotorCodeSegmentFilterCategory.Sequence, Display = "序列号" },
        ];
    }

    #endregion
}
