using Prism.Ioc;
using Prism.Modularity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceModule
{
    /// <summary>
    /// 设备模块 - 交互事件：注册设备与 UI 交互事件配置管理页面。
    /// </summary>
    public class ModuleInteractiveEvents : IModule
    {
        #region ===================== IModule =====================

        /// <summary> 模块初始化完成回调（当前无额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册交互事件视图到导航系统 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<Views.InteractiveEventsView>(); // 交互事件配置页
        }

        #endregion
    }
}
