using WorkmanshipModule.ViewModels;
using WorkmanshipModule.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace WorkmanshipModule
{
    /// <summary>
    /// 数据采集配置模块：注册 DataCollectConfigView 及其 ViewModel。
    /// </summary>
    public class ModuleDataCollectConfig : IModule
    {
        #region ===================== IModule 实现 =====================

        /// <summary> 模块初始化（当前无需额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册导航视图与 ViewModel 绑定 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<DataCollectConfigView, DataCollectConfigViewModel>(); // 注册数据采集配置页
        }

        #endregion
    }
}
