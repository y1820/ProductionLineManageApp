using System.Windows.Controls;

namespace DeviceModule.Views.Dialog
{
    /// <summary>
    /// 编辑设备连接弹窗视图：修改已有工位 PLC 连接参数，由 EditDeviceConnectViewModel 驱动。
    /// </summary>
    public partial class EditDeviceConnectView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化编辑设备连接弹窗 XAML </summary>
        public EditDeviceConnectView()
        {
            InitializeComponent(); // 加载布局并绑定 ViewModel
        }

        #endregion
    }
}
