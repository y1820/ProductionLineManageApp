using System.Windows.Controls;

namespace DeviceModule.Views.Dialog
{
    /// <summary>
    /// 编辑地址映射弹窗视图：修改已有 PLC 地址映射，由 EditAddressMappingViewModel 驱动。
    /// </summary>
    public partial class EditAddressMappingView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化编辑地址映射弹窗 XAML </summary>
        public EditAddressMappingView()
        {
            InitializeComponent(); // 加载布局并绑定 ViewModel
        }

        #endregion
    }
}
