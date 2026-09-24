using Prism.Ioc;
using Prism.Modularity;

namespace ProductionModule
{
    /// <summary>
    /// 生产模块 - 下发产品型号：注册产品型号下发页面到 Prism 导航系统。
    /// </summary>
    public class ModuleIssueProductModel : IModule
    {
        #region ===================== IModule =====================

        /// <summary> 模块初始化完成回调（当前无额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册产品型号下发视图到导航系统 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<Views.IssueProductModelView, ViewModels.IssueProductModelViewModel>(); // 下发产品型号页
        }

        #endregion
    }
}
