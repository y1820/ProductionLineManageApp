using System.Windows.Controls;
using WorkmanshipModule.ViewModels.Dialog;

namespace WorkmanshipModule.Views.Dialog
{
    /// <summary>
    /// 编辑型号弹窗视图：修改型号名称与下发代号，绑定 EditTypeViewModel。
    /// </summary>
    public partial class EditTypeView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化编辑型号弹窗并注入 ViewModel </summary>
        public EditTypeView(EditTypeViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            DataContext = viewModel; // 绑定弹窗 ViewModel
        }

        #endregion
    }
}
