using System.Windows.Controls;

namespace DeviceModule.Views
{
    /// <summary>
    /// 设备看板视图：按产线分组展示各工位设备连接状态卡片，逻辑由 DeviceDashboardViewModel 驱动。
    /// </summary>
    public partial class DeviceDashboardView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化设备看板 XAML 布局 </summary>
        public DeviceDashboardView()
        {
            InitializeComponent(); // 加载 XAML 并建立 DataContext 绑定
        }

        #endregion
    }
}
