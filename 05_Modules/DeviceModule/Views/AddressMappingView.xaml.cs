using System.Windows.Controls;

namespace DeviceModule.Views
{
    /// <summary>
    /// 地址映射管理视图：展示/筛选 PLC 地址映射列表，增删改通过弹窗完成。
    /// </summary>
    public partial class AddressMappingView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化地址映射 XAML 布局 </summary>
        public AddressMappingView()
        {
            InitializeComponent(); // 加载 XAML 并绑定 AddressMappingViewModel
        }

        #endregion
    }
}
