using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ProductionLineManage.Shared.Infrastructure
{
    /// <summary>
    /// 全局未处理异常捕获（三层）：
    /// 1. DispatcherUnhandledException — UI 线程（可 e.Handled=true 阻止崩溃）
    /// 2. AppDomain.UnhandledException — 后台线程 / 非 Task 路径
    /// 3. TaskScheduler.UnobservedTaskException — fire-and-forget Task 未观察异常
    /// </summary>
    public static class GlobalExceptionHandler
    {
        #region ===================== 字段 =====================

        private static bool _initialized;

        #endregion

        #region ===================== 公开方法 =====================

        /// <summary>
        /// 尽早调用（建议在 App 静态构造或 OnStartup 第一行）。
        /// 注册 AppDomain + Task 未观察异常；不依赖 WPF Application 是否已创建。
        /// </summary>
        public static void Initialize(string? logDirectory = null)
        {
            if (_initialized)
                return;

            _initialized = true;

            if (!string.IsNullOrWhiteSpace(logDirectory))
                ExceptionToastManager.LogDirectory = logDirectory;

            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        /// <summary>
        /// 在 base.OnStartup 之后调用，此时 Application.Current 已就绪。
        /// </summary>
        public static void EnsureDispatcherHandler()
        {
            var app = Application.Current;
            if (app == null)
                return;

            app.DispatcherUnhandledException -= OnDispatcherUnhandledException;
            app.DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        #endregion

        #region ===================== 异常回调 =====================

        /// <summary>UI 线程未处理异常</summary>
        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                HandleException("UI 线程未处理异常", e.Exception);
                e.Handled = true;
            }
            catch
            {
                e.Handled = true;
            }
        }

        /// <summary>AppDomain 未处理异常（后台线程或致命）</summary>
        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var title = e.IsTerminating
                    ? "AppDomain 未处理异常（致命，进程可能退出）"
                    : "AppDomain 未处理异常（后台线程）";

                if (e.ExceptionObject is Exception ex)
                    HandleException(title, ex);
                else
                    SafeShowToast(title, e.ExceptionObject?.ToString() ?? "未知异常对象");
            }
            catch
            {
                // 处理器自身不得再抛出
            }
        }

        /// <summary>Task 未观察异常</summary>
        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                HandleException("Task 未观察异常", e.Exception);
                e.SetObserved();
            }
            catch
            {
                try { e.SetObserved(); } catch { /* ignored */ }
            }
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary>统一处理：写日志 + 显示提示</summary>
        private static void HandleException(string title, Exception exception)
        {
            try
            {
                WriteApplicationLog(title, exception);
                SafeShowToast(title, exception.Message, exception);
            }
            catch
            {
                // 日志/弹窗失败时仍不向外抛
            }
        }

        /// <summary>安全显示异常提示（自身不抛出）</summary>
        private static void SafeShowToast(string title, string message, Exception? exception = null)
        {
            try
            {
                ExceptionToastManager.Show(title, message, exception);
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>写入 Application 日志目录</summary>
        private static void WriteApplicationLog(string title, Exception exception)
        {
            try
            {
                var appLogDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "Application");
                Directory.CreateDirectory(appLogDir);
                var appLogFile = Path.Combine(appLogDir, $"{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(appLogFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [Fatal] [GlobalExceptionHandler] {title} | {exception}{Environment.NewLine}");
            }
            catch
            {
                // ignored
            }
        }

        #endregion
    }
}
