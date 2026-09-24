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

namespace ProductionModule.Views
{
    /// <summary>
    /// 下发产品型号视图：向产线/工站下发当前生产产品型号配置。
    /// </summary>
    public partial class IssueProductModelView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化下发产品型号视图 </summary>
        public IssueProductModelView()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion
    }
}
