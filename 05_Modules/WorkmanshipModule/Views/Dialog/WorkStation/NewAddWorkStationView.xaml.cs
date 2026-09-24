using System.Windows.Controls;
using WorkmanshipModule.ViewModels.Dialog;

namespace WorkmanshipModule.Views.Dialog
{
    /// <summary>
    /// 新增工位弹窗视图：录入工位名称与所属产线，绑定 NewAddWorkStationViewModel。
    /// </summary>
    public partial class NewAddWorkStationView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化新增工位弹窗并注入 ViewModel </summary>
        public NewAddWorkStationView(NewAddWorkStationViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            DataContext = viewModel; // 绑定弹窗 ViewModel
        }

        #endregion
    }
}
