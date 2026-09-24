using Prism.Ioc;
using Prism.Modularity;

namespace WorkmanshipModule
{
    /// <summary>
    /// 工艺流程模块：注册 ProcessFlowView 导航页面。
    /// </summary>
    public class ModuleProcessFlow : IModule
    {
        #region ===================== IModule 实现 =====================

        /// <summary> 模块初始化（当前无需额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册导航视图类型 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<Views.ProcessFlowView>(); // 注册工艺流程页
        }

        #endregion
    }
}
