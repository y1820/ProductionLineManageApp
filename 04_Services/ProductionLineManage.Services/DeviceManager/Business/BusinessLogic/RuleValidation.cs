using ProductionLineManage.Core.Enums;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Services.DeviceManager.Business;

namespace ProductionLineManage.Services.DeviceManager.Business.BusinessLogic
{
    /// <summary>
    /// 条码编码规则校验：按 RuleType 校验左/右/中间/年月日/总长度等规则。
    /// 多组规则（GroupNo）为或关系，组内为且关系。
    /// </summary>
    public class RuleValidation : IRuleValidation
    {
        #region ===================== 单条规则校验 =====================

        /// <summary> 校验单条规则与条码是否匹配 </summary>
        public (bool passed, string message) Validate(BaseRulesItem rule, string code)
        {
            if (string.IsNullOrEmpty(code))
                return (false, "条码为空");

            var passed = (CodeRuleType)rule.RuleType switch
            {
                CodeRuleType.Left => ValidateLeft(code, rule),
                CodeRuleType.Right => ValidateRight(code, rule),
                CodeRuleType.Middle => ValidateMiddle(code, rule),
                CodeRuleType.Year => ValidateYear(code, rule),
                CodeRuleType.Month => ValidateMonth(code, rule),
                CodeRuleType.Day => ValidateDay(code, rule),
                CodeRuleType.TotalLength => ValidateTotalLength(code, rule),
                _ => true // 未知规则类型默认通过
            };

            if (passed)
                return (true, string.Empty);

            return (false, $"规则Id={rule.Id}，规则名称={rule.RuleName}");
        }

        #endregion

        #region ===================== 规则组校验 =====================

        /// <summary> 按 GroupNo 分组校验：任一组全部通过即成功，否则返回最后一组失败信息 </summary>
        public (bool passed, string message) ValidateRules(IEnumerable<BaseRulesItem> rules, string code)
        {
            if (string.IsNullOrEmpty(code))
                return (false, "条码为空");

            if (rules == null || !rules.Any())
                return (true, string.Empty); // 无规则视为通过

            var rulesList = rules.ToList();
            string? lastFailure = null;

            foreach (var group in rulesList.GroupBy(r => r.GroupNo))
            {
                var allPassed = true;
                foreach (var rule in group)
                {
                    var (passed, message) = Validate(rule, code);
                    if (!passed)
                    {
                        allPassed = false;
                        lastFailure = message;
                        break; // 组内一条失败则整组失败
                    }
                }

                if (allPassed)
                    return (true, string.Empty); // 任一组全通过即可
            }

            return (false, lastFailure ?? "编码规则验证不通过");
        }

        #endregion

        #region ===================== 位置规则 =====================

        /// <summary> 左匹配：条码前缀等于 RuleContent </summary>
        private static bool ValidateLeft(string code, BaseRulesItem rule)
        {
            var length = rule.Length > 0 ? rule.Length : rule.RuleContent?.Length ?? 0;
            if (code.Length < length) return false;

            var leftPart = code.Substring(0, length);
            return leftPart == rule.RuleContent;
        }

        /// <summary> 右匹配：条码后缀等于 RuleContent </summary>
        private static bool ValidateRight(string code, BaseRulesItem rule)
        {
            var length = rule.Length > 0 ? rule.Length : rule.RuleContent?.Length ?? 0;
            if (code.Length < length) return false;

            var rightPart = code.Substring(code.Length - length);
            return rightPart == rule.RuleContent;
        }

        /// <summary> 中间匹配：从 StartBit 起取 Length 位与 RuleContent 比较 </summary>
        private static bool ValidateMiddle(string code, BaseRulesItem rule)
        {
            if (rule.StartBit <= 0 || rule.Length <= 0) return false;
            if (code.Length < rule.StartBit + rule.Length - 1) return false;

            var middlePart = code.Substring(rule.StartBit - 1, rule.Length); // StartBit 为 1-based
            return middlePart == rule.RuleContent;
        }

        /// <summary> 总长度匹配：条码长度等于 rule.Length </summary>
        private static bool ValidateTotalLength(string code, BaseRulesItem rule)
        {
            return code.Length == rule.Length;
        }

        #endregion

        #region ===================== 日期规则 =====================

        /// <summary> 日匹配：指定位置片段等于当前日（D2） </summary>
        private static bool ValidateDay(string code, BaseRulesItem rule)
        {
            if (rule.StartBit <= 0 || rule.Length <= 0) return false;
            if (code.Length < rule.StartBit + rule.Length - 1) return false;

            var dayPart = code.Substring(rule.StartBit - 1, rule.Length);
            var currentDay = DateTime.Now.Day.ToString("D2");

            return dayPart == currentDay;
        }

        /// <summary> 月匹配：指定位置片段等于当前月（D2） </summary>
        private static bool ValidateMonth(string code, BaseRulesItem rule)
        {
            if (rule.StartBit <= 0 || rule.Length <= 0) return false;
            if (code.Length < rule.StartBit + rule.Length - 1) return false;

            var monthPart = code.Substring(rule.StartBit - 1, rule.Length);
            var currentMonth = DateTime.Now.Month.ToString("D2");

            return monthPart == currentMonth;
        }

        /// <summary> 年匹配：指定位置片段等于 RuleContent 中当前年份映射值 </summary>
        private static bool ValidateYear(string code, BaseRulesItem rule)
        {
            if (rule.StartBit <= 0 || rule.Length <= 0) return false;
            if (code.Length < rule.StartBit + rule.Length - 1) return false;

            var yearPart = code.Substring(rule.StartBit - 1, rule.Length);
            var currentYear = DateTime.Now.Year;

            var mappings = ParseYearMapping(rule.RuleContent);

            if (mappings.TryGetValue(currentYear, out var expectedValue))
                return yearPart == expectedValue;

            return false; // 当前年无映射则失败
        }

        /// <summary> 解析年份映射字符串，格式如 {2024:A,2025:B} </summary>
        private static Dictionary<int, string> ParseYearMapping(string ruleContent)
        {
            var result = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(ruleContent)) return result;

            var content = ruleContent.Trim('{', '}');
            var pairs = content.Split(',');

            foreach (var pair in pairs)
            {
                var kv = pair.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[0], out var year))
                    result[year] = kv[1];
            }

            return result;
        }

        #endregion
    }
}
