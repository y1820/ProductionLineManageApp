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

namespace ProductionLineManage.Shared.Controls
{
    /// <summary>加载指示控件，支持自定义提示文本</summary>
    public partial class Loading : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary>初始化加载控件</summary>
        public Loading()
        {
            InitializeComponent();
        }

        #endregion

        #region ===================== 依赖属性 =====================

        /// <summary>提示文本依赖属性</summary>
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(Loading), new PropertyMetadata("正在加载..."));

        /// <summary>提示文本</summary>
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        #endregion
    }
}
