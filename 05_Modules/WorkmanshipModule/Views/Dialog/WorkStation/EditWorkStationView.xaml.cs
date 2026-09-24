using System.Windows.Controls;
using WorkmanshipModule.ViewModels.Dialog;

namespace WorkmanshipModule.Views.Dialog
{
    /// <summary>
    /// 编辑工位弹窗视图：修改工位名称与所属产线，绑定 EditWorkStationViewModel。
    /// </summary>
    public partial class EditWorkStationView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化编辑工位弹窗并注入 ViewModel </summary>
        public EditWorkStationView(EditWorkStationViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            DataContext = viewModel; // 绑定弹窗 ViewModel
        }

        #endregion
    }
}
