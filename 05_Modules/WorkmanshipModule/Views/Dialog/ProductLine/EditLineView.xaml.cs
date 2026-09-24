using System.Windows.Controls;
using WorkmanshipModule.ViewModels.Dialog;

namespace WorkmanshipModule.Views.Dialog
{
    /// <summary>
    /// 编辑产线弹窗视图：修改产线名称与备注，绑定 EditLineViewModel。
    /// </summary>
    public partial class EditLineView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化编辑产线弹窗并注入 ViewModel </summary>
        public EditLineView(EditLineViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            DataContext = viewModel; // 绑定弹窗 ViewModel
        }

        #endregion
    }
}
