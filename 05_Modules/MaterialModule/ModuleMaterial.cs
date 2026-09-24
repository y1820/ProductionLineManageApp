using Prism.Ioc;
using Prism.Modularity;

namespace MaterialModule
{
    /// <summary>
    /// 物料模块 - 物料管理：注册物料主数据页面到 Prism 导航系统。
    /// </summary>
    public class ModuleMaterial : IModule
    {
        #region ===================== IModule =====================

        /// <summary> 模块初始化完成回调（当前无额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册物料视图到导航系统 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<Views.MaterialView>(); // 物料列表页
        }

        #endregion
    }
}
