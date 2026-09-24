using System.Windows.Controls;
using WorkmanshipModule.ViewModels.Dialog;

namespace WorkmanshipModule.Views.Dialog
{
    /// <summary>
    /// 编辑工艺流程弹窗视图：修改型号/产线/工位关联，绑定 EditProcessViewModel。
    /// </summary>
    public partial class EditProcessView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化编辑工艺流程弹窗并注入 ViewModel </summary>
        public EditProcessView(EditProcessViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            DataContext = viewModel; // 绑定弹窗 ViewModel
        }

        #endregion
    }
}
