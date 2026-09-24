using System.Windows.Controls;

namespace DeviceModule.Views
{
    /// <summary>
    /// 设备连接配置视图：管理工位 PLC 连接参数，支持新增/编辑/删除连接信息。
    /// </summary>
    public partial class DeviceConnectInfoView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化设备连接配置 XAML 布局 </summary>
        public DeviceConnectInfoView()
        {
            InitializeComponent(); // 加载 XAML 并绑定 DeviceConnectInfoViewModel
        }

        #endregion
    }
}
