namespace ProductionLineManage.Core.Services.DataLoadGrop
{
    /// <summary> 启动数据加载服务接口；由 DataLoadViewModel 在弹窗打开后调用 </summary>
    public interface IDataLoadService
    {
        #region ===================== 全量加载 =====================

        /// <summary>
        /// 从数据库加载全部配置表，通过 progress 报告进度。
        /// 每项加载完成后发布对应 *UpdatedEvent 并写入缓存。
        /// </summary>
        Task LoadAllConfigurationsAsync(IProgress<(int percent, string message)> progress);

        #endregion
    }
}
