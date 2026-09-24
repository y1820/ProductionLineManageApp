using ProductionLineManage.Core.Events;
using ProductionLineManage.Core.Models.DataBase;
using ProductionLineManage.Core.Models.MotorCode;
using ProductionLineManage.Core.Services.DataLoadGrop;
using ProductionLineManage.Core.Services.MotorCode;
using ProductionLineManage.Infrastructure.Data.Repository;
using Prism.Events;

namespace ProductionLineManage.Services.MotorCode
{
    /// <summary>
    /// 电机码配置缓存：与 DataLoadService 启动加载、各配置页保存后刷新联动。
    /// 快照包含序列配置、日期映射、固定片段、规则与片段绑定五张表。
    /// </summary>
    public sealed class MotorCodeCacheService : IMotorCodeCacheService
    {
        #region ===================== 字段 =====================

        /// <summary> 数据库访问 </summary>
        private readonly SQLHelper _sql;
        /// <summary> 全局内存缓存 </summary>
        private readonly IDataCacheService _cache;
        /// <summary> 配置更新事件发布 </summary>
        private readonly IEventAggregator _eventAggregator;

        #endregion

        #region ===================== 构造 =====================

        /// <summary> 注入 SQL、缓存与事件聚合器 </summary>
        public MotorCodeCacheService(
            SQLHelper sql,
            IDataCacheService cache,
            IEventAggregator eventAggregator)
        {
            _sql = sql;
            _cache = cache;
            _eventAggregator = eventAggregator;
        }

        #endregion

        #region ===================== 缓存读写 =====================

        /// <inheritdoc />
        public bool IsLoaded => _cache.HasData<MotorCodeCacheSnapshot>(); // 是否已加载过快照

        /// <inheritdoc />
        public MotorCodeCacheSnapshot GetSnapshot()
        {
            if (_cache.HasData<MotorCodeCacheSnapshot>())
                return _cache.GetData<MotorCodeCacheSnapshot>(); // 返回已缓存快照

            return new MotorCodeCacheSnapshot(); // 未加载时返回空快照
        }

        /// <inheritdoc />
        public async Task RefreshAsync()
        {
            var snapshot = await LoadFromDatabaseAsync(); // 重新查库组装
            _cache.SetData(snapshot); // 写入全局缓存
            _eventAggregator.GetEvent<MotorCodeConfigUpdatedEvent>().Publish(snapshot); // 通知订阅方
        }

        #endregion

        #region ===================== 数据库加载 =====================

        /// <summary> 从数据库组装快照（表不存在时返回空快照，不抛错） </summary>
        internal static async Task<MotorCodeCacheSnapshot> LoadFromDatabaseAsync(SQLHelper sql)
        {
            var snapshot = new MotorCodeCacheSnapshot();

            try
            {
                snapshot.SequenceConfigs = (await sql.QueryAsync<craft_MotorCodeSequenceConfig>(
                    "SELECT * FROM craft_MotorCodeSequenceConfig")).ToList(); // 序列号配置

                snapshot.DateMaps = (await sql.QueryAsync<craft_MotorCodeDateMap>(
                    "SELECT * FROM craft_MotorCodeDateMap")).ToList(); // 年月日代号映射

                snapshot.FixedSegments = (await sql.QueryAsync<craft_MotorCodeFixedSegment>(
                    "SELECT * FROM craft_MotorCodeFixedSegment")).ToList(); // 固定片段

                snapshot.Rules = (await sql.QueryAsync<craft_MotorCodeRule>(
                    "SELECT * FROM craft_MotorCodeRule")).ToList(); // 规则公式

                snapshot.SegmentBinds = (await sql.QueryAsync<craft_MotorCodeSegmentBind>(
                    "SELECT * FROM craft_MotorCodeSegmentBind")).ToList(); // 片段绑定
            }
            catch
            {
                // 未建表或首次部署：返回空快照，生成服务会给出明确错误
            }

            return snapshot;
        }

        /// <summary> 使用实例 SQL 加载快照 </summary>
        private Task<MotorCodeCacheSnapshot> LoadFromDatabaseAsync() =>
            LoadFromDatabaseAsync(_sql);

        #endregion
    }
}
