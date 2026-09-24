using System.Windows.Controls;
using WorkmanshipModule.ViewModels.Dialog;

namespace WorkmanshipModule.Views.Dialog
{
    /// <summary>
    /// 新增工艺流程弹窗视图：录入型号/产线/工位关联，绑定 NewAddProcessViewModel。
    /// </summary>
    public partial class NewAddProcessView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化新增工艺流程弹窗并注入 ViewModel </summary>
        public NewAddProcessView(NewAddProcessViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            DataContext = viewModel; // 绑定弹窗 ViewModel
        }

        #endregion
    }
}
