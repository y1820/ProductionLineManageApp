using ProductionLineManage.Shared.Controls;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ProductionLineManage.Shared.Infrastructure
{
    /// <summary>右下角异常抽屉弹窗管理器（非模态，可堆叠，手动关闭后消失）</summary>
    public static class ExceptionToastManager
    {
        #region ===================== 字段 =====================

        private static readonly object LockObj = new();
        private static ExceptionToastHostWindow? _hostWindow;
        private static string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Unhandled");

        #endregion

        #region ===================== 公开属性 =====================

        /// <summary>未处理异常日志目录</summary>
        public static string LogDirectory
        {
            get => _logDirectory;
            set
            {
                _logDirectory = value;
                EnsureLogDirectory();
            }
        }

        #endregion

        #region ===================== 公开方法 =====================

        /// <summary>显示异常提示抽屉并写入日志</summary>
        public static void Show(string title, string message, Exception? exception = null)
        {
            WriteLog(title, message, exception);

            var app = Application.Current;
            if (app == null)
                return;

            if (app.Dispatcher.CheckAccess())
                ShowInternal(title, message);
            else
                app.Dispatcher.BeginInvoke(() => ShowInternal(title, message));
        }

        /// <summary>关闭宿主窗口并释放资源</summary>
        public static void Shutdown()
        {
            var app = Application.Current;
            if (app == null)
                return;

            void CloseHost()
            {
                lock (LockObj)
                {
                    if (_hostWindow == null)
                        return;

                    _hostWindow.Close();
                    _hostWindow = null;
                }
            }

            if (app.Dispatcher.CheckAccess())
                CloseHost();
            else
                app.Dispatcher.Invoke(CloseHost);
        }

        /// <summary>写入未处理异常日志文件</summary>
        public static void WriteLog(string title, string message, Exception? exception = null)
        {
            try
            {
                EnsureLogDirectory();
                var logFile = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
                var sb = new StringBuilder();
                sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ");
                sb.Append("[Unhandled] ");
                sb.Append(title);
                sb.Append(" | ");
                sb.Append(message);

                if (exception != null)
                {
                    sb.AppendLine();
                    sb.Append(exception);
                }

                sb.AppendLine();
                lock (LockObj)
                {
                    File.AppendAllText(logFile, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // 日志写入失败时不影响主程序
            }
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary>UI 线程内显示提示</summary>
        private static void ShowInternal(string title, string message)
        {
            lock (LockObj)
            {
                _hostWindow ??= new ExceptionToastHostWindow();
                if (!_hostWindow.IsVisible)
                    _hostWindow.Show();

                _hostWindow.AddToast(title, message);
                _hostWindow.UpdatePosition();
            }
        }

        /// <summary>确保日志目录存在</summary>
        private static void EnsureLogDirectory()
        {
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);
        }

        #endregion
    }

    /// <summary>异常提示宿主窗口（透明、置顶、右下角堆叠）</summary>
    internal sealed class ExceptionToastHostWindow : Window
    {
        #region ===================== 字段 =====================

        private readonly StackPanel _toastStack;

        #endregion

        #region ===================== 构造 =====================

        /// <summary>初始化透明宿主窗口</summary>
        public ExceptionToastHostWindow()
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            Width = 396;
            SizeToContent = SizeToContent.Height;
            MaxHeight = 560;
            IsHitTestVisible = true;

            _toastStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Content = new Grid
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 16, 16),
                Children = { _toastStack }
            };

            Loaded += (_, _) => UpdatePosition();
            SourceInitialized += (_, _) => UpdatePosition();
        }

        #endregion

        #region ===================== 公开方法 =====================

        /// <summary>添加一条异常提示</summary>
        public void AddToast(string title, string message)
        {
            var toast = new ExceptionToastControl
            {
                Title = title,
                Message = TruncateMessage(message),
                OccurredAtText = DateTime.Now.ToString("HH:mm:ss")
            };

            toast.Closed += (_, _) =>
            {
                _toastStack.Children.Remove(toast);
                UpdatePosition();
                if (_toastStack.Children.Count == 0)
                    Hide();
            };

            _toastStack.Children.Add(toast);
            UpdateLayout();
            UpdatePosition();
        }

        /// <summary>更新窗口位置到工作区右下角</summary>
        public void UpdatePosition()
        {
            var workArea = SystemParameters.WorkArea;
            const double margin = 16;

            UpdateLayout();
            Left = workArea.Right - Width - margin;
            Top = workArea.Bottom - ActualHeight - margin;
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary>截断过长消息文本</summary>
        private static string TruncateMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "发生未知异常，详情请查看 Logs 目录。";

            const int maxLength = 260;
            return message.Length <= maxLength ? message : message[..maxLength] + "...";
        }

        #endregion
    }
}
