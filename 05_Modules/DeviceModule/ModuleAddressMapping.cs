using DeviceModule.ViewModels;
using DeviceModule.Views;
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
    /// 设备模块 - 地址映射：注册 PLC 数据地址与业务变量映射管理页面。
    /// </summary>
    public class ModuleAddressMapping : IModule
    {
        #region ===================== IModule =====================

        /// <summary> 模块初始化完成回调（当前无额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册地址映射视图到导航系统 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<AddressMappingView, AddressMappingViewModel>(); // 地址映射配置页
        }

        #endregion
    }
}
