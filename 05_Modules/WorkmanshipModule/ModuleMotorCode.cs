using WorkmanshipModule.ViewModels;
using WorkmanshipModule.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace WorkmanshipModule
{
    /// <summary>
    /// 电机码配置模块：注册日期映射、固定段、规则、流水号四个导航页面。
    /// </summary>
    public class ModuleMotorCode : IModule
    {
        #region ===================== IModule 实现 =====================

        /// <summary> 模块初始化（当前无需额外逻辑） </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        /// <summary> 注册电机码相关导航视图与 ViewModel 绑定 </summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<MotorCodeDateMapView, MotorCodeDateMapViewModel>(); // 日期映射页
            containerRegistry.RegisterForNavigation<MotorCodeFixedSegmentView, MotorCodeFixedSegmentViewModel>(); // 固定段页
            containerRegistry.RegisterForNavigation<MotorCodeRuleView, MotorCodeRuleViewModel>(); // 规则页
            containerRegistry.RegisterForNavigation<MotorCodeSequenceView, MotorCodeSequenceViewModel>(); // 流水号页
        }

        #endregion
    }
}
