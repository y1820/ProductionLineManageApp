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
    /// 登录视图：启动阶段用户身份验证入口。
    /// </summary>
    public partial class LoginView : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化登录视图 </summary>
        public LoginView(LoginViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            DataContext = viewModel; // 绑定 ViewModel 以驱动登录表单与命令
        }

        #endregion
    }
}
