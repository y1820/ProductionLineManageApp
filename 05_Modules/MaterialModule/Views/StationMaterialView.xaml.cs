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
    /// 工站物料视图：工站与物料 BOM 关联的查询与管理界面。
    /// </summary>
    public partial class StationMaterialView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化工站物料视图 </summary>
        public StationMaterialView()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion
    }
}
