using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ProductionLineManage.Shared.Controls
{
    /// <summary>右下角异常提示抽屉控件，支持滑入/滑出动画与手动关闭</summary>
    public partial class ExceptionToastControl : UserControl
    {
        #region ===================== 依赖属性 =====================

        /// <summary>标题依赖属性</summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ExceptionToastControl),
                new PropertyMetadata("程序异常"));

        /// <summary>消息内容依赖属性</summary>
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(ExceptionToastControl),
                new PropertyMetadata(string.Empty));

        /// <summary>发生时间文本依赖属性</summary>
        public static readonly DependencyProperty OccurredAtTextProperty =
            DependencyProperty.Register(nameof(OccurredAtText), typeof(string), typeof(ExceptionToastControl),
                new PropertyMetadata(string.Empty));

        /// <summary>标题</summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>异常消息内容</summary>
        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        /// <summary>发生时间（格式化文本）</summary>
        public string OccurredAtText
        {
            get => (string)GetValue(OccurredAtTextProperty);
            set => SetValue(OccurredAtTextProperty, value);
        }

        #endregion

        #region ===================== 事件 =====================

        /// <summary>控件关闭后触发</summary>
        public event EventHandler? Closed;

        #endregion

        #region ===================== 构造 =====================

        /// <summary>初始化异常提示控件</summary>
        public ExceptionToastControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        #endregion

        #region ===================== 公开方法 =====================

        /// <summary>关闭提示（播放滑出动画后触发 Closed）</summary>
        public void Dismiss()
        {
            if (Resources["ExceptionToastSlideOutStoryboard"] is Storyboard storyboard)
            {
                storyboard.Completed += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
                storyboard.Begin(this);
            }
            else
            {
                Closed?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region ===================== 事件处理 =====================

        /// <summary>加载完成后播放滑入动画</summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Resources["ExceptionToastSlideInStoryboard"] is Storyboard storyboard)
                storyboard.Begin(this);
        }

        /// <summary>关闭按钮点击</summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Dismiss();
        }

        #endregion
    }
}
