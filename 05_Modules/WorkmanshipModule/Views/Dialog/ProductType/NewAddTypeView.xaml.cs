using System.Windows.Controls;
using WorkmanshipModule.ViewModels.Dialog;

namespace WorkmanshipModule.Views.Dialog
{
    /// <summary>
    /// 新增型号弹窗视图：录入型号名称与下发代号，绑定 NewAddTypeViewModel。
    /// </summary>
    public partial class NewAddTypeView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化新增型号弹窗并注入 ViewModel </summary>
        public NewAddTypeView(NewAddTypeViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            DataContext = viewModel; // 绑定弹窗 ViewModel
        }

        #endregion
    }
}
