using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaterialModule
{
    /// <summary>
    /// 物料模块 - 工站物料：注册工站与物料关联管理页面到 Prism 导航系统。
    /// </summary>
    public class ModuleStationMaterial : IModule
    {
        #region ===================== IModule =====================

        /// <summary> 模块初始化完成回调（当前无额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册工站物料视图到导航系统 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<Views.StationMaterialView>(); // 工站物料列表页
        }

        #endregion
    }
}
