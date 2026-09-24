using ProductionLineManage.Core.Models.MotorCode;

namespace ProductionLineManage.Core.Services.MotorCode
{
    /// <summary> 电机码配置缓存：启动加载、UI 保存后刷新、供生成服务读取 </summary>
    public interface IMotorCodeCacheService
    {
        #region ===================== 缓存生命周期 =====================

        /// <summary> 从数据库重新加载并写入 IDataCacheService，同时发布更新事件 </summary>
        Task RefreshAsync();

        /// <summary> 获取当前缓存快照（可能为空壳） </summary>
        MotorCodeCacheSnapshot GetSnapshot();

        /// <summary> 缓存是否已加载 </summary>
        bool IsLoaded { get; }

        #endregion
    }
}
