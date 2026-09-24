using System;
using System.Windows;
using System.Windows.Controls;

namespace DeviceModule.Views
{
    /// <summary>
    /// 交互事件监控视图：实时展示设备通信日志与交互事件，支持查看详情。
    /// </summary>
    public partial class InteractiveEventsView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化交互事件监控 XAML 布局 </summary>
        public InteractiveEventsView()
        {
            InitializeComponent(); // 加载 XAML 并绑定 InteractiveEventsViewModel
        }

        #endregion

        #region ===================== 事件处理 =====================

        /// <summary> 日期控件变更回调（预留 UI 线程刷新入口） </summary>
        private void Readdate(object sender, EventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() => { }); // 确保在 UI 线程执行
        }

        #endregion
    }
}
