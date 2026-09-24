using System.Windows.Controls;

namespace DeviceModule.Views.Dialog
{
    /// <summary>
    /// 新增地址映射弹窗视图：录入 PLC 地址、数据类型与工位关联，由 AddAddressMappingViewModel 驱动。
    /// </summary>
    public partial class AddAddressMappingView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化新增地址映射弹窗 XAML </summary>
        public AddAddressMappingView()
        {
            InitializeComponent(); // 加载布局并绑定 ViewModel
        }

        #endregion
    }
}
