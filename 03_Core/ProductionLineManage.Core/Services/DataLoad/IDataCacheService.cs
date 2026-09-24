namespace ProductionLineManage.Core.Services.DataLoadGrop
{
    /// <summary> 启动数据泛型缓存服务：以类型 T 为键，实现配置表复用读写 </summary>
    public interface IDataCacheService
    {
        #region ===================== 泛型缓存读写 =====================

        /// <summary> 写入指定类型的缓存数据 </summary>
        void SetData<T>(T data);

        /// <summary> 读取指定类型的缓存数据 </summary>
        T GetData<T>();

        /// <summary> 是否已缓存指定类型 </summary>
        bool HasData<T>();

        /// <summary> 清除指定类型的缓存 </summary>
        void ClearData<T>();

        /// <summary> 清除全部缓存 </summary>
        void ClearAll();

        #endregion
    }
}
