using Prism.Ioc;
using Prism.Modularity;
using DeviceModule.ViewModels;
using DeviceModule.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeviceModule
{
    /// <summary>
    /// 设备模块 - 设备连接信息：注册 PLC/设备连接配置管理页面。
    /// </summary>
    public class ModuleDeviceConnectInfo : IModule
    {
        #region ===================== IModule =====================

        /// <summary> 模块初始化完成回调（当前无额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册设备连接信息视图到导航系统 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<DeviceConnectInfoView, DeviceConnectInfoViewModel>(); // 设备连接配置页
        }

        #endregion
    }
}
