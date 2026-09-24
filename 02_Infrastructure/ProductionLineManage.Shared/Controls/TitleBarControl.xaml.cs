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
    /// <summary>自定义标题栏控件：窗口拖动、最小化/最大化/关闭、可配置图标与背景</summary>
    public partial class TitleBarControl : UserControl
    {
        #region ===================== 构造 =====================

        /// <summary>初始化标题栏控件</summary>
        public TitleBarControl()
        {
            InitializeComponent();
        }

        #endregion

        #region ===================== 依赖属性 =====================

        /// <summary>标题文本依赖属性</summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(TitleName), typeof(string), typeof(TitleBarControl),
                new PropertyMetadata("应用程序"));

        /// <summary>标题文本</summary>
        public string TitleName
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>字体图标依赖属性</summary>
        public static readonly DependencyProperty FontIconProperty =
            DependencyProperty.Register(nameof(FontIcon), typeof(string), typeof(TitleBarControl),
                new PropertyMetadata("🖥"));

        /// <summary>字体图标（Emoji 或 Segoe MDL2 字符）</summary>
        public string FontIcon
        {
            set => SetValue(FontIconProperty, value);
            get => (string)GetValue(FontIconProperty);
        }

        /// <summary>图标路径依赖属性</summary>
        public static readonly DependencyProperty IconSourceProperty =
            DependencyProperty.Register(nameof(IconSource), typeof(ImageSource), typeof(TitleBarControl),
                new PropertyMetadata(null));

        /// <summary>图标图片源</summary>
        public ImageSource IconSource
        {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        /// <summary>标题栏背景色依赖属性</summary>
        public static readonly DependencyProperty TitleBarBackgroundProperty =
            DependencyProperty.Register(nameof(TitleBarBackground), typeof(Brush), typeof(TitleBarControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(45, 45, 48))));

        /// <summary>标题栏背景色</summary>
        public Brush TitleBarBackground
        {
            get => (Brush)GetValue(TitleBarBackgroundProperty);
            set => SetValue(TitleBarBackgroundProperty, value);
        }

        /// <summary>是否显示最小化按钮依赖属性</summary>
        public static readonly DependencyProperty ShowMinimizeButtonProperty =
            DependencyProperty.Register(nameof(ShowMinimizeButton), typeof(bool), typeof(TitleBarControl),
                new PropertyMetadata(true));

        /// <summary>是否显示最小化按钮</summary>
        public bool ShowMinimizeButton
        {
            get => (bool)GetValue(ShowMinimizeButtonProperty);
            set => SetValue(ShowMinimizeButtonProperty, value);
        }

        /// <summary>是否显示最大化按钮依赖属性</summary>
        public static readonly DependencyProperty ShowMaximizeButtonProperty =
            DependencyProperty.Register(nameof(ShowMaximizeButton), typeof(bool), typeof(TitleBarControl),
                new PropertyMetadata(true));

        /// <summary>是否显示最大化按钮</summary>
        public bool ShowMaximizeButton
        {
            get => (bool)GetValue(ShowMaximizeButtonProperty);
            set => SetValue(ShowMaximizeButtonProperty, value);
        }

        #endregion

        #region ===================== 路由事件 =====================

        /// <summary>关闭窗口路由事件</summary>
        public static readonly RoutedEvent CloseWindowEvent =
            EventManager.RegisterRoutedEvent(nameof(CloseWindow), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(TitleBarControl));

        /// <summary>关闭窗口事件（外部可拦截）</summary>
        public event RoutedEventHandler CloseWindow
        {
            add => AddHandler(CloseWindowEvent, value);
            remove => RemoveHandler(CloseWindowEvent, value);
        }

        #endregion

        #region ===================== 事件处理 =====================

        /// <summary>最小化按钮点击</summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        /// <summary>最大化/还原按钮点击</summary>
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.WindowState = window.WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;

                UpdateMaximizeButtonIcon(window.WindowState);
            }
        }

        /// <summary>关闭按钮点击</summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(CloseWindowEvent));

            var window = Window.GetWindow(this);
            window?.Close();
        }

        #endregion

        #region ===================== 辅助方法 =====================

        /// <summary>根据窗口状态更新最大化按钮图标</summary>
        private void UpdateMaximizeButtonIcon(WindowState state)
        {
            MaximizeButton.Content = state == WindowState.Maximized ? "\xE923" : "\xE922";
        }

        /// <summary>父窗口状态变化时同步按钮图标</summary>
        public void OnParentWindowStateChanged(WindowState newState)
        {
            UpdateMaximizeButtonIcon(newState);
        }

        #endregion

        #region ===================== 鼠标拖动 =====================

        /// <summary>支持单击拖动与双击最大化/还原</summary>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (e.ClickCount == 2)
            {
                var window = Window.GetWindow(this);
                if (window != null && window.ResizeMode != ResizeMode.NoResize)
                {
                    window.WindowState = window.WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;
                }
            }
            else
            {
                var window = Window.GetWindow(this);
                window?.DragMove();
            }
        }

        #endregion
    }
}
