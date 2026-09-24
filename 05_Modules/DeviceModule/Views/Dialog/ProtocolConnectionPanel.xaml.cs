using System.Windows.Controls;

namespace DeviceModule.Views.Dialog
{
    /// <summary>
    /// 协议连接参数面板：按 S7/OPC UA 等协议展示连接字段录入 UI，供设备连接弹窗复用。
    /// </summary>
    public partial class ProtocolConnectionPanel : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化协议连接参数面板 XAML </summary>
        public ProtocolConnectionPanel()
        {
            InitializeComponent(); // 加载布局，字段绑定由父 ViewModel 提供
        }

        #endregion
    }
}
