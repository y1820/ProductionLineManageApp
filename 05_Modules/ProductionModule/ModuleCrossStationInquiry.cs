using Prism.Ioc;
using Prism.Modularity;
using ProductionModule.ViewModels;
using ProductionModule.Views;

namespace ProductionModule
{
    /// <summary>
    /// 生产模块 - 过站记录查询：注册过站记录查询页面到 Prism 导航系统。
    /// </summary>
    public class ModuleCrossStationInquiry : IModule
    {
        #region ===================== IModule =====================

        /// <summary> 模块初始化完成回调（当前无额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册过站记录查询视图到导航系统 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<CrossStationInquiryView, CrossStationInquiryViewModel>(); // 过站记录查询页
        }

        #endregion
    }
}
