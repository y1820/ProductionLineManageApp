using Prism.Ioc;
using Prism.Modularity;

namespace MaterialModule
{
    /// <summary>
    /// 物料模块 - 编码规则：注册编码规则管理页面到 Prism 导航系统。
    /// </summary>
    public class ModuleCodeRules : IModule
    {
        #region ===================== IModule =====================

        /// <summary> 模块初始化完成回调（当前无额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册编码规则视图到导航系统 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<Views.CodeRulesView>(); // 编码规则列表页
        }

        #endregion
    }
}
