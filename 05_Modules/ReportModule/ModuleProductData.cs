using Prism.Ioc;
using Prism.Modularity;
using ReportModule.ViewModels;
using ReportModule.Views;

namespace ReportModule
{
    /// <summary>
    /// 报表模块 - 产品加工数据：注册产品加工历史、物料绑定、过站记录等报表页面。
    /// </summary>
    public class ModuleProductData : IModule
    {
        #region ===================== IModule =====================

        /// <summary> 模块初始化完成回调（当前无额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册报表视图到导航系统 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<ProductDataView, ProductDataViewModel>(); // 产品加工数据（report_ProcessHistory）
            containerRegistry.RegisterForNavigation<MaterialBindView, MaterialBindViewModel>(); // 物料绑定报表
            containerRegistry.RegisterForNavigation<StationPassRecordView, StationPassRecordViewModel>(); // 过站记录报表
        }

        #endregion
    }
}
