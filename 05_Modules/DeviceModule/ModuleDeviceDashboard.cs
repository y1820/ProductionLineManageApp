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
    /// 设备模块 - 设备仪表盘：注册设备运行状态总览页面。
    /// </summary>
    public class ModuleDeviceDashboard : IModule
    {
        #region ===================== IModule =====================

        /// <summary> 模块初始化完成回调（当前无额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册设备仪表盘视图到导航系统 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<Views.DeviceDashboardView>(); // 设备仪表盘页
        }

        #endregion
    }
}
