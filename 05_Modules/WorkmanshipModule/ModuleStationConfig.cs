using Prism.Ioc;
using Prism.Modularity;

namespace WorkmanshipModule
{
    /// <summary>
    /// 工位配置模块：注册 StationConfigView 导航页面。
    /// </summary>
    public class ModuleStationConfig : IModule
    {
        #region ===================== IModule 实现 =====================

        /// <summary> 模块初始化（当前无需额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册导航视图类型 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<Views.StationConfigView>(); // 注册工位配置页
        }

        #endregion
    }
}
