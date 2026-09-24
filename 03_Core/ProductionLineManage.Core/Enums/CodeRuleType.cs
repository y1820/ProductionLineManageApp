// ProductionLineManage.Core/Enums/CodeRuleType.cs
namespace ProductionLineManage.Core.Enums
{
    /// <summary>
    /// 编码规则类型
    /// </summary>
    public enum CodeRuleType
    {
        /// <summary>左侧内容验证</summary>
        Left = 1,

        /// <summary>右侧内容验证</summary>
        Right = 2,

        /// <summary>中间内容验证</summary>
        Middle = 3,

        /// <summary>年代码验证</summary>
        Year = 4,

        /// <summary>月代码验证</summary>
        Month = 5,

        /// <summary>日代码验证</summary>
        Day = 6,

        /// <summary>总长度验证</summary>
        TotalLength = 7
    }
}
