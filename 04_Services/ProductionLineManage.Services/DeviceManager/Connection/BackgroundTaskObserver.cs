namespace ProductionLineManage.Services.DeviceManager.Connection
{
    /// <summary>
    /// StopAsync 等待线程超时后，观察后台 Task 避免未观察异常。
    /// </summary>
    internal static class BackgroundTaskObserver
    {
        #region ===================== 观察入口 =====================

        /// <summary>  fire-and-forget 观察 Task 完成 </summary>
        public static void Observe(Task? task)
        {
            if (task == null)
                return;

            _ = ObserveAsync(task);
        }

        #endregion

        #region ===================== 内部实现 =====================

        /// <summary> 等待 Task 完成并吞掉异常，避免 UnobservedTaskException </summary>
        private static async Task ObserveAsync(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // 仅观察，不向上抛
            }
        }

        #endregion
    }
}
