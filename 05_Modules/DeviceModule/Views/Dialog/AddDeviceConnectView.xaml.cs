using System.Windows.Controls;

namespace DeviceModule.Views.Dialog
{
    /// <summary>
    /// 新增设备连接弹窗视图：配置工位 PLC 连接参数，由 AddDeviceConnectViewModel 驱动。
    /// </summary>
    public partial class AddDeviceConnectView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化新增设备连接弹窗 XAML </summary>
        public AddDeviceConnectView()
        {
            InitializeComponent(); // 加载布局并绑定 ViewModel
        }

        #endregion
    }
}
