using ProductionLineManage.Core.Models.DataBase;

namespace ProductionLineManage.Core.Services.DeviceManager.Business
{
    /// <summary> 编码规则验证接口 </summary>
    public interface IRuleValidation
    {
        #region ===================== 规则验证 =====================

        /// <summary> 验证单条规则 </summary>
        (bool passed, string message) Validate(BaseRulesItem rule, string code);

        /// <summary> 验证一组规则（同组 AND，不同组 OR） </summary>
        (bool passed, string message) ValidateRules(IEnumerable<BaseRulesItem> rules, string code);

        #endregion
    }
}
