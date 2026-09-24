using ProductionLineManage.Host.SetupModule.ViewModels;
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

namespace ProductionLineManage.Host.SetupModule.Views
{
    /// <summary>
    /// 数据加载视图：启动阶段展示系统初始化与数据加载进度。
    /// </summary>
    public partial class DataLoadView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化数据加载视图 </summary>
        public DataLoadView(DataLoadViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            DataContext = viewModel; // 绑定 ViewModel 以驱动加载状态与命令
        }

        #endregion
    }
}
