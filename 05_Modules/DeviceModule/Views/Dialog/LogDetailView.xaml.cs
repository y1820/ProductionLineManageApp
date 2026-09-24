using System.Windows.Controls;

namespace DeviceModule.Views.Dialog
{
    /// <summary>
    /// 设备日志详情弹窗视图：展示单条通信日志的命令、响应与异常，由 LogDetailViewModel 驱动。
    /// </summary>
    public partial class LogDetailView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化日志详情弹窗 XAML </summary>
        public LogDetailView()
        {
            InitializeComponent(); // 加载布局并绑定 ViewModel
        }

        #endregion
    }
}
