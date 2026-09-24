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
    /// 物料视图：物料主数据的查询、新增、编辑与管理界面。
    /// </summary>
    public partial class MaterialView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化物料视图 </summary>
        public MaterialView()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion
    }
}
