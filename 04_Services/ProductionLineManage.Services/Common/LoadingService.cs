// ProductionLineManage.Services/Common/LoadingService.cs
using ProductionLineManage.Core.Services.LoadingAnimationGrop;
using ProductionLineManage.Shared.Controls;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ProductionLineManage.Services.Common
{
    /// <summary>
    /// 全局加载遮罩服务：支持引用计数式 Show/Hide，以及包装异步操作的 ExecuteAsync。
    /// </summary>
    public class LoadingService : ILoadingService
    {
        #region ===================== 字段 =====================

        /// <summary> 线程同步锁，保护 _loadingCount 与 UI 状态 </summary>
        private readonly object _lockObj = new object();

        /// <summary> 覆盖面板，显示时遮挡底层交互 </summary>
        private Panel? _overlayPanel;

        /// <summary> 加载动画控件（Loading） </summary>
        private UserControl? _loadingControl;

        /// <summary> 加载引用计数，支持嵌套 Show/Hide </summary>
        private int _loadingCount;

        #endregion

        #region ===================== 公开方法 =====================

        /// <summary> 绑定遮罩面板与加载控件 </summary>
        public void Initialize(Panel overlayPanel, UserControl loadingControl)
        {
            _overlayPanel = overlayPanel;
            _loadingControl = loadingControl;
            if (_loadingControl != null)
            {
                _loadingControl.Visibility = Visibility.Collapsed; // 初始隐藏
            }
        }

        /// <summary> 显示加载提示（默认文案「加载中...」） </summary>
        public void ShowMessage(string message = "加载中...") => Show(message);

        /// <summary> 隐藏加载提示 </summary>
        public void HideMessage() => Hide();

        /// <summary> 包装异步操作并自动 Show/Hide，返回结果 </summary>
        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, string message = "加载中...")
        {
            try
            {
                Show(message); // 操作前显示遮罩
                return await action();
            }
            finally
            {
                Hide(); // 无论成功失败均隐藏
            }
        }

        /// <summary> 包装无返回值异步操作并自动 Show/Hide </summary>
        public async Task ExecuteAsync(Func<Task> action, string message = "加载中...")
        {
            try
            {
                Show(message);
                await action();
            }
            finally
            {
                Hide();
            }
        }

        #endregion

        #region ===================== 私有方法 =====================

        /// <summary> 显示遮罩，引用计数 +1；首次显示时更新文案并禁用底层 </summary>
        private void Show(string message = "加载中...")
        {
            Application.Current.Dispatcher.Invoke(() => // 必须在 UI 线程操作
            {
                lock (_lockObj)
                {
                    _loadingCount++;
                    if (_loadingCount == 1) // 首次 Show 才真正显示
                    {
                        if (_loadingControl != null && _overlayPanel != null)
                        {
                            (_loadingControl as Loading)!.Text = message; // 设置提示文本
                            _overlayPanel.Visibility = Visibility.Visible;
                            _loadingControl.Visibility = Visibility.Visible;
                            _overlayPanel.IsEnabled = false; // 禁止底层点击
                        }
                    }
                }
            });
        }

        /// <summary> 隐藏遮罩，引用计数 -1；归零后恢复底层交互 </summary>
        private void Hide()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lockObj)
                {
                    _loadingCount--;
                    if (_loadingCount <= 0)
                    {
                        _loadingCount = 0; // 防止负数
                        if (_loadingControl != null && _overlayPanel != null)
                        {
                            _overlayPanel.Visibility = Visibility.Collapsed;
                            _loadingControl.Visibility = Visibility.Collapsed;
                            _overlayPanel.IsEnabled = true; // 恢复底层交互
                        }
                    }
                }
            });
        }

        #endregion
    }
}
