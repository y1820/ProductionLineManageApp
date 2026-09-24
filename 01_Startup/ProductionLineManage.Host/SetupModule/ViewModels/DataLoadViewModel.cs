using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Infrastructure.Logging;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ProductionLineManage.Host.SetupModule.ViewModels
{
    /// <summary>
    /// 数据加载弹窗 ViewModel（启动流程第 2 步）。
    /// 弹窗打开后自动调用 DataLoadService.LoadAllConfigurationsAsync，
    /// 完成后以 OK 关闭；MainViewModel 收到 OK 后发布 DataLoadCompletedEvent。
    /// </summary>
    public class DataLoadViewModel : BindableBase, IDialogAware
    {
        #region ===================== 私有字段 =====================

        /// <summary>从数据库加载配置并发布到事件/缓存的服务</summary>
        private readonly IDataLoadService _dataLoadService;

        /// <summary>异常日志</summary>
        private readonly ILogger _logger;

        /// <summary>防止 OnDialogOpened 重复触发加载</summary>
        private bool _loadStarted;

        /// <summary>是否正在加载（加载中禁止用户关闭弹窗）</summary>
        private bool _isLoading;

        #endregion

        #region ===================== 构造 =====================

        /// <summary>注入数据加载服务与日志</summary>
        public DataLoadViewModel(IDataLoadService dataLoadService, ILogger logger)
        {
            _dataLoadService = dataLoadService;
            _logger = logger;
        }

        #endregion

        #region ===================== 绑定属性 =====================

        /// <summary>进度条数值 0~100</summary>
        private double _progressValue;

        /// <summary>进度条绑定属性</summary>
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        /// <summary>当前正在加载的配置项名称</summary>
        private string _currentTask = "";

        /// <summary>进度说明文字，如「正在加载：型号信息」</summary>
        public string CurrentTask
        {
            get => _currentTask;
            set => SetProperty(ref _currentTask, value);
        }

        #endregion

        #region ===================== 异步加载 =====================

        /// <summary>
        /// Prism 弹窗打开回调。延迟到 Loaded 优先级后启动 LoadDataAsync，
        /// 确保 RequestClose 事件已被 DialogService 订阅。
        /// </summary>
        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (_loadStarted) // 防止重复打开时重复加载
                return;

            _loadStarted = true;
            // 延迟到 DialogWindow.Loaded 之后，确保 RequestClose 已绑定
            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => _ = LoadDataAsync()));
        }

        /// <summary>
        /// 核心加载逻辑：调用 DataLoadService，更新进度，成功/失败后关闭弹窗。
        /// </summary>
        private async Task LoadDataAsync()
        {
            _isLoading = true; // 加载中禁止用户关闭弹窗

            // 在 UI 线程更新进度条与文字
            var progress = new Progress<(int percent, string message)>(update =>
            {
                try
                {
                    ProgressValue = update.percent;
                    CurrentTask = update.message;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }
            });

            try
            {
                // 逐表查库 → 发布 *UpdatedEvent → 写入 IDataCacheService
                await _dataLoadService.LoadAllConfigurationsAsync(progress);
                CloseDialog(ButtonResult.OK); // 全部成功：MainViewModel 将发布 DataLoadCompletedEvent
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                HandyControl.Controls.MessageBox.Show(
                    $"数据加载异常：{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                CloseDialog(ButtonResult.Cancel); // 失败以 Cancel 关闭
            }
        }

        /// <summary>重置加载状态并通知 Prism 关闭弹窗</summary>
        /// <param name="result">OK=加载成功，Cancel=失败或中断</param>
        private void CloseDialog(ButtonResult result)
        {
            _isLoading = false;
            RequestClose?.Invoke(new DialogResult(result));
        }

        #endregion

        #region ===================== IDialogAware =====================

        /// <summary>Prism 关闭弹窗通道</summary>
        public event Action<IDialogResult>? RequestClose;

        /// <summary>弹窗标题</summary>
        public string Title => "数据加载中";

        /// <summary>加载进行中不允许用户关闭（防止半加载状态进入主界面）</summary>
        public bool CanCloseDialog() => !_isLoading;

        /// <summary>弹窗关闭后清理（当前无额外逻辑）</summary>
        public void OnDialogClosed()
        {
        }

        #endregion
    }
}
