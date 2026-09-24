using System.Windows.Controls;

namespace ProductionLineManage.Core.Services.LoadingAnimationGrop
{
    /// <summary> 全局加载动画遮罩服务接口 </summary>
    public interface ILoadingService
    {
        #region ===================== 初始化 =====================

        /// <summary> 绑定遮罩面板与加载控件（应用启动时调用一次） </summary>
        void Initialize(Panel overlayPanel, UserControl loadingControl);

        #endregion

        #region ===================== 带遮罩的异步执行 =====================

        /// <summary> 执行异步操作并显示加载动画 </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="action">异步操作</param>
        /// <param name="message">显示的提示文字</param>
        /// <returns>操作结果</returns>
        Task<T> ExecuteAsync<T>(Func<Task<T>> action, string message = "加载中...");

        /// <summary> 执行异步操作并显示加载动画（无返回值） </summary>
        Task ExecuteAsync(Func<Task> action, string message = "加载中...");

        #endregion

        #region ===================== 手动显示/隐藏 =====================

        /// <summary> 显示加载遮罩（需配对 HideMessage，或由 ExecuteAsync 自动管理） </summary>
        void ShowMessage(string message = "加载中...");

        /// <summary> 隐藏加载遮罩 </summary>
        void HideMessage();

        #endregion
    }
}
