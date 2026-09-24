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

namespace MaterialModule.Views.Dialog
{
    /// <summary>
    /// 新增编码规则对话框视图：创建新的条码/序列号编码规则。
    /// </summary>
    public partial class AddCodeRuleView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化新增编码规则对话框 </summary>
        public AddCodeRuleView()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion
    }
}
