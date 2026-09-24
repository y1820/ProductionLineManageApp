using System.Windows.Controls;
using WorkmanshipModule.ViewModels.Dialog;

namespace WorkmanshipModule.Views.Dialog
{
    /// <summary>
    /// 新增产线弹窗视图：录入产线名称与备注，绑定 NewAddLineViewModel。
    /// </summary>
    public partial class NewAddLineView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化新增产线弹窗并注入 ViewModel </summary>
        public NewAddLineView(NewAddLineViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            DataContext = viewModel; // 绑定弹窗 ViewModel
        }

        #endregion
    }
}
