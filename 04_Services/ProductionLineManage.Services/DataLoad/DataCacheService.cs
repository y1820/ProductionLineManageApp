using System.Collections.Concurrent;
using ProductionLineManage.Core.Services.DataLoadGrop;

namespace ProductionLineManage.Services.DataLoadGroup
{
    /// <summary>
    /// 数据缓存服务：启动加载时写入、各模块按需读取；
    /// 解决 Prism 事件订阅晚于发布导致的数据丢失问题。
    /// </summary>
    public class DataCacheService : IDataCacheService
    {
        #region ===================== 字段 =====================

        /// <summary> 按实体类型存储的全局内存缓存 </summary>
        private readonly ConcurrentDictionary<Type, object> _cache = new ConcurrentDictionary<Type, object>();

        #endregion

        #region ===================== 缓存读写 =====================

        /// <summary> 写入指定类型的缓存数据 </summary>
        public void SetData<T>(T data)
        {
            _cache[typeof(T)] = data!; // 以泛型类型为 Key 覆盖写入
        }

        /// <summary> 读取指定类型的缓存数据，未命中返回 default </summary>
        public T GetData<T>()
        {
            if (_cache.TryGetValue(typeof(T), out var data)) // 尝试按类型取缓存
            {
                return (T)data; // 强转后返回
            }
            return default!; // 未加载过该类型
        }

        /// <summary> 判断指定类型是否已有缓存 </summary>
        public bool HasData<T>()
        {
            return _cache.ContainsKey(typeof(T)); // 检查 Key 是否存在
        }

        /// <summary> 清除指定类型的缓存 </summary>
        public void ClearData<T>()
        {
            _cache.TryRemove(typeof(T), out _); // 移除单类型条目
        }

        /// <summary> 清空全部缓存 </summary>
        public void ClearAll()
        {
            _cache.Clear(); // 重置整个字典
        }

        #endregion
    }
}
