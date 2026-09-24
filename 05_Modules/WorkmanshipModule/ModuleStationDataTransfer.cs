using WorkmanshipModule.ViewModels;
using WorkmanshipModule.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace WorkmanshipModule
{
    /// <summary>
    /// 工位传值配置模块：注册 StationDataTransferView 及其 ViewModel。
    /// </summary>
    public class ModuleStationDataTransfer : IModule
    {
        #region ===================== IModule 实现 =====================

        /// <summary> 模块初始化（当前无需额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册导航视图与 ViewModel 绑定 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<StationDataTransferView, StationDataTransferViewModel>(); // 注册工位传值页
        }

        #endregion
    }
}
