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

namespace MaterialModule.Views
{
    /// <summary>
    /// 编码规则视图：物料条码/序列号编码规则的查询与管理界面。
    /// </summary>
    public partial class CodeRulesView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化编码规则视图 </summary>
        public CodeRulesView()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion
    }
}
