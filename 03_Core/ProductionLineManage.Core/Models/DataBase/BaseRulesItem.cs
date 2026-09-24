namespace ProductionLineManage.Core.Models.DataBase
{
    /// <summary> 编码规则基类（物料码/流水码规则共用） </summary>
    public class BaseRulesItem : BaseEntity
    {
        #region ===================== 规则标识 =====================

        /// <summary> 组号（同一物料下不同规则组） </summary>
        public int GroupNo { get; set; }

        /// <summary> 规则类型（参见 CodeRuleType 枚举） </summary>
        public int RuleType { get; set; }

        /// <summary> 规则名称 </summary>
        public string RuleName { get; set; } = string.Empty;

        #endregion

        #region ===================== 规则内容 =====================

        /// <summary> 起始位 </summary>
        public int StartBit { get; set; }

        /// <summary> 长度 </summary>
        public int Length { get; set; }

        /// <summary> 规则内容 </summary>
        public string RuleContent { get; set; } = string.Empty;

        /// <summary> 示例编码（用于界面预览） </summary>
        public string ExampleCode { get; set; } = string.Empty;

        /// <summary> 是否启用 </summary>
        public bool IsEnabled { get; set; }

        #endregion
    }
}
