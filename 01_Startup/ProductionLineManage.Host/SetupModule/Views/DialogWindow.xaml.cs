using Prism.Services.Dialogs;
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
using System.Windows.Shapes;

namespace ProductionLineManage.Host.SetupModule.Views
{
    /// <summary>
    /// Prism 对话框宿主窗口：承载 IDialogService 弹出的自定义对话框内容。
    /// </summary>
    public partial class DialogWindow : Window, IDialogWindow
    {
        #region ===================== 构造 =====================

        /// <summary> 初始化对话框窗口 </summary>
        public DialogWindow()
        {
            InitializeComponent(); // 加载 XAML 布局
        }

        #endregion

        #region ===================== IDialogWindow =====================

        /// <summary> 对话框关闭时的返回结果（确认/取消及参数） </summary>
        public IDialogResult? Result { get; set; }

        #endregion
    }
}
