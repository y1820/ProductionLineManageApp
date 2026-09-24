using ProductionLineManage.Core.Services.LoadingAnimationGrop;
using ProductionLineManage.Host.SetupModule.ViewModels;
using ProductionLineManage.Services.Common;
using ProductionLineManage.Shared.Infrastructure;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace ProductionLineManage.Host.SetupModule.Views
{
    /// <summary>
    /// 主窗口：启动流程入口在 Loaded → RunStartupFlow；退出流程在 Closing / PerformShutdownAsync。
    /// </summary>
    public partial class MainView : Window
    {
        #region ===================== 私有字段 =====================

        /// <summary> 全屏遮罩与加载动画服务（关闭 Web 时显示「正在关闭…」） </summary>
        private readonly ILoadingService _loadingService;

        /// <summary> 主窗口 ViewModel，负责登录/数据加载弹窗编排 </summary>
        private readonly MainViewModel _viewModel;

        /// <summary> 是否已进入正式关闭流程（防止 Closing 重复拦截、重复弹确认框） </summary>
        private bool _isShuttingDown;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 构造主窗口，绑定 ViewModel 并订阅启动/关闭相关事件 </summary>
        public MainView(ILoadingService loadingService, MainViewModel viewModel)
        {
            InitializeComponent(); // 加载 XAML 布局
            _viewModel = viewModel; // 保存 ViewModel 引用
            DataContext = viewModel; // 绑定 DataContext 供 XAML 绑定
            SourceInitialized += MainWindow_SourceInitialized!; // 订阅 Win32 窗口初始化以拦截系统命令
            Loaded += MainView_Loaded; // 订阅首次加载以启动流程
            Closing += MainView_Closing; // 订阅关闭事件以拦截并确认退出
            _loadingService = loadingService; // 保存遮罩服务引用
            _viewModel.ShutdownRequested += OnShutdownRequested; // 订阅 ViewModel 退出请求（登录取消、数据加载失败等）
        }

        #endregion

        #region ===================== 启动与关闭 =====================

        /// <summary>
        /// ViewModel 请求退出应用。
        /// skipConfirmation=true：用户已在登录/数据加载明确取消，直接走 PerformShutdownAsync；
        /// skipConfirmation=false：走 Close()，由 Closing 弹出「是否退出」确认。
        /// </summary>
        private void OnShutdownRequested(bool skipConfirmation)
        {
            if (_isShuttingDown) // 已在关闭中则忽略重复请求
                return;

            if (skipConfirmation)
            {
                _ = PerformShutdownAsync(); // 登录/数据加载取消：跳过二次确认，直接优雅关闭
                return;
            }

            Close(); // 普通退出：触发 Closing 事件，由用户确认
        }

        /// <summary>
        /// 主窗口首次 Loaded：初始化遮罩服务，启动「登录 → 数据加载」流程。
        /// 放在 Loaded 而非构造函数，避免 CreateShell 期间弹模态框导致异常。
        /// </summary>
        private void MainView_Loaded(object sender, RoutedEventArgs e)
        {
            _loadingService.Initialize(this.LoadingOverlay, this.loading); // 绑定 XAML 中的 LoadingOverlay 控件
            _viewModel.RunStartupFlow(); // 触发启动流程（ShowLoginDialog）
        }

        /// <summary>
        /// 用户点击主窗口关闭按钮或 Alt+F4 时触发。
        /// 首次拦截并确认；确认后异步停止服务再 Shutdown。
        /// </summary>
        private void MainView_Closing(object? sender, CancelEventArgs e)
        {
            if (_isShuttingDown) // 已在 PerformShutdownAsync 中，允许窗口关闭
                return;

            e.Cancel = true; // 先取消本次关闭，改为手动控制退出时机

            if (HandyControl.Controls.MessageBox.Show("Exit?", "Message", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return; // 用户选择「否」则保持运行

            _ = PerformShutdownAsync(); // 用户确认退出：异步停服务后再 Shutdown
        }

        /// <summary>
        /// 优雅关闭：显示遮罩 → 停止设备与 Kestrel → Shutdown。
        /// 登录取消、数据加载失败、主窗口确认退出均走此路径。
        /// </summary>
        private async Task PerformShutdownAsync()
        {
            if (_isShuttingDown) // 防止重复进入
                return;

            _isShuttingDown = true; // 标记已进入关闭流程
            var stopDevices = _viewModel.ShouldStopDevicesOnShutdown; // 是否需要停止设备与 Web 服务
            _loadingService.ShowMessage(stopDevices
                ? "正在关闭 Web 服务，请稍候..."
                : "正在退出...");
            try
            {
                var timeout = stopDevices ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(8); // 按场景设置超时
                using var cts = new CancellationTokenSource(timeout);
                await _viewModel.StopServicesAsync(cts.Token, stopDevices); // 停止后台服务
            }
            catch (OperationCanceledException)
            {
                // 超时仍继续退出，OnExit / ProcessExit 会再次兜底停止 Web 服务
            }
            finally
            {
                ExceptionToastManager.Shutdown(); // 关闭异常抽屉
                Application.Current.Shutdown(); // 结束 WPF 应用（触发 App.OnExit）
                // 保持遮罩至进程退出，不在此处 HideMessage
            }
        }

        #endregion

        #region ===================== 窗口最大化和还原处理右上角图标 =====================

        private const int WM_SYSCOMMAND = 0x0112; // Win32 系统命令消息
        private const int SC_MAXIMIZE = 0xF030; // 最大化命令
        private const int SC_RESTORE = 0xF120; // 还原命令
        private const int SC_MINIMIZE = 0xF020; // 最小化命令

        /// <summary> 窗口句柄初始化后挂载 Win32 消息钩子 </summary>
        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle; // 获取窗口句柄
            HwndSource source = HwndSource.FromHwnd(handle); // 获取 HwndSource
            source.AddHook(new HwndSourceHook(WndProc)); // 添加消息处理钩子
        }

        /// <summary> 处理右键点击标题栏菜单中的最大化/还原，同步右上角按钮图标 </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SYSCOMMAND) // 系统命令消息
            {
                int command = wParam.ToInt32() & 0xFFF0; // 提取命令码

                switch (command)
                {
                    case SC_MAXIMIZE:
                        Console.WriteLine("接收到最大化命令");
                        MaxWind.Content = "\ue65b"; // 切换为还原图标
                        break;

                    case SC_RESTORE:
                        Console.WriteLine("接收到还原命令");
                        MaxWind.Content = "\ue60d"; // 切换为最大化图标
                        break;

                    case SC_MINIMIZE:
                        Console.WriteLine("接收到最小化命令");
                        break;
                }
            }

            return IntPtr.Zero; // 未处理则继续传递
        }

        /// <summary> 最大化/还原按钮点击 </summary>
        private void MaxWindow_btn(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized) // 当前已最大化则还原
            {
                WindowState = WindowState.Normal;
                MaxWind.Content = "\ue60d"; // 切换为最大化图标
            }
            else // 当前未最大化则最大化
            {
                WindowState = WindowState.Maximized;
                MaxWind.Content = "\ue65b"; // 切换为还原图标
            }
        }

        /// <summary> 最小化按钮点击 </summary>
        private void MinWindow_btn(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized; // 最小化窗口
        }

        #endregion

        #region ===================== 标题栏按钮事件 =====================

        /// <summary> 关闭按钮点击 </summary>
        private void CloseWindow_btn(object sender, RoutedEventArgs e)
        {
            Close(); // 触发 Closing 流程
        }

        /// <summary> 系统消息按钮点击（预留） </summary>
        private void Message_btn(object sender, RoutedEventArgs e)
        {
        }

        /// <summary> 打开关联的 ContextMenu（如下拉菜单按钮） </summary>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button; // 定位到按钮下方
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true; // 显示菜单
            }
        }

        #endregion
    }
}
