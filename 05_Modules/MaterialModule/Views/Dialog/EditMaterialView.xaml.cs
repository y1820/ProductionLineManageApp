using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MaterialModule.ViewModels.Dialog;

namespace MaterialModule.Views.Dialog
{
    /// <summary>
    /// 编辑物料对话框视图：修改已有物料主数据信息。
    /// </summary>
    public partial class EditMaterialView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化编辑物料对话框 </summary>
        public EditMaterialView(EditMaterialViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            this.DataContext = viewModel; // 绑定 ViewModel 以驱动表单与保存命令
        }

        #endregion
    }
}
