using ProductionLineManage.Infrastructure.Data.Repository;
using Dapper;
using System.Text;
using ProductionLineManage.Core.Services.RepositoryGrop;
using ProductionLineManage.Core.Models.DataBase;

namespace ProductionLineManage.Services.Repositories
{
    /// <summary>
    /// 泛型仓储实现：基于 SQLHelper 提供 CRUD 及自定义 SQL 扩展，T 须继承 BaseEntity。
    /// </summary>
    /// <typeparam name="T">须继承 BaseEntity 的实体类型</typeparam>
    public class Repository<T> : IRepository<T> where T : BaseEntity, new()
    {
        #region ===================== 字段与构造 =====================

        /// <summary> SQL 帮助类实例 </summary>
        protected readonly SQLHelper _sqlHelper;

        /// <summary> 表名（与实体类名一致） </summary>
        protected readonly string _tableName;

        /// <summary> 带 schema 的完整表名，默认 [dbo].[表名] </summary>
        protected readonly string _tableNameWithSchema;

        /// <summary> 注入 SQLHelper 并解析表名 </summary>
        public Repository(SQLHelper sqlHelper)
        {
            _sqlHelper = sqlHelper;
            _tableName = typeof(T).Name; // 实体类名即表名
            _tableNameWithSchema = $"[dbo].[{_tableName}]"; // 默认 dbo schema
        }

        #endregion

        #region ===================== 查询 =====================

        /// <summary> 查询全部记录（同步） </summary>
        public virtual IEnumerable<T> GetAll()
        {
            string sql = $"SELECT * FROM {_tableNameWithSchema}";
            return _sqlHelper.Query<T>(sql);
        }

        /// <summary> 查询全部记录（异步） </summary>
        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            string sql = $"SELECT * FROM {_tableNameWithSchema}";
            return await _sqlHelper.QueryAsync<T>(sql);
        }

        /// <summary> 按主键 Id 查询单条（同步） </summary>
        public virtual T? GetById(int id)
        {
            string sql = $"SELECT * FROM {_tableNameWithSchema} WHERE Id = @Id";
            return _sqlHelper.QuerySingle<T>(sql, new { Id = id });
        }

        /// <summary> 按主键 Id 查询单条（异步） </summary>
        public virtual async Task<T?> GetByIdAsync(int id)
        {
            string sql = $"SELECT * FROM {_tableNameWithSchema} WHERE Id = @Id";
            return await _sqlHelper.QuerySingleAsync<T>(sql, new { Id = id });
        }

        /// <summary> 判断指定 Id 是否存在（同步） </summary>
        public virtual bool Exists(int id)
        {
            string sql = $"SELECT COUNT(1) FROM {_tableNameWithSchema} WHERE Id = @Id";
            return _sqlHelper.ExecuteScalar<int>(sql, new { Id = id }) > 0;
        }

        /// <summary> 判断指定 Id 是否存在（异步） </summary>
        public virtual async Task<bool> ExistsAsync(int id)
        {
            string sql = $"SELECT COUNT(1) FROM {_tableNameWithSchema} WHERE Id = @Id";
            var count = await _sqlHelper.ExecuteScalarAsync<int>(sql, new { Id = id });
            return count > 0;
        }

        #endregion

        #region ===================== 新增 =====================

        /// <summary> 新增实体（同步），自动填充 CreateTime 并回写 Id </summary>
        /// <param name="entity">待插入实体</param>
        /// <returns>新记录 Id</returns>
        public virtual int Insert(T entity)
        {
            // 自动设置创建时间
            if (entity.CreateTime == null)
            {
                entity.CreateTime = DateTime.Now;
            }

            // 反射生成 INSERT 语句（排除 Id，CreateTime 由上方或数据库处理）
            var properties = typeof(T).GetProperties()
                .Where(p => p.Name != "Id" && p.CanWrite);

            var columns = new List<string>();
            var parameters = new DynamicParameters();

            foreach (var prop in properties)
            {
                var columnName = prop.Name;
                var value = prop.GetValue(entity);

                columns.Add(columnName);
                parameters.Add($"@{columnName}", value);
            }

            var columnList = string.Join(", ", columns);
            var valueList = string.Join(", ", columns.Select(c => "@" + c));

            string sql = $@"
                INSERT INTO {_tableNameWithSchema} ({columnList}) 
                VALUES ({valueList});
                SELECT CAST(SCOPE_IDENTITY() as int);
            ";

            var newId = _sqlHelper.ExecuteScalar<int>(sql, parameters); // 取自增 Id
            entity.Id = newId; // 回写实体
            return newId;
        }

        /// <summary> 新增实体（异步） </summary>
        public virtual async Task<int> InsertAsync(T entity)
        {
            if (entity.CreateTime == null)
            {
                entity.CreateTime = DateTime.Now;
            }

            var properties = typeof(T).GetProperties()
                .Where(p => p.Name != "Id" && p.CanWrite);

            var columns = new List<string>();
            var parameters = new DynamicParameters();

            foreach (var prop in properties)
            {
                var columnName = prop.Name;
                var value = prop.GetValue(entity);

                columns.Add(columnName);
                parameters.Add($"@{columnName}", value);
            }

            var columnList = string.Join(", ", columns);
            var valueList = string.Join(", ", columns.Select(c => "@" + c));

            string sql = $@"
                INSERT INTO {_tableNameWithSchema} ({columnList}) 
                VALUES ({valueList});
                SELECT CAST(SCOPE_IDENTITY() as int);
            ";

            var newId = await _sqlHelper.ExecuteScalarAsync<int>(sql, parameters);
            entity.Id = newId;
            return newId;
        }

        #endregion

        #region ===================== 更新 =====================

        /// <summary> 更新实体（同步），自动刷新 UpdateTime </summary>
        /// <param name="entity">含 Id 的实体</param>
        /// <returns>受影响行数</returns>
        public virtual int Update(T entity)
        {
            entity.UpdateTime = DateTime.Now; // 自动更新时间戳

            var properties = typeof(T).GetProperties()
                .Where(p => p.Name != "Id" && p.Name != "CreateTime" && p.CanWrite); // 不更新主键与创建时间

            var setClauses = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("@Id", entity.Id);

            foreach (var prop in properties)
            {
                var columnName = prop.Name;
                var value = prop.GetValue(entity);

                setClauses.Add($"{columnName} = @{columnName}");
                parameters.Add($"@{columnName}", value);
            }

            var setClause = string.Join(", ", setClauses);
            string sql = $"UPDATE {_tableNameWithSchema} SET {setClause} WHERE Id = @Id";

            return _sqlHelper.Execute(sql, parameters);
        }

        /// <summary> 更新实体（异步） </summary>
        public virtual async Task<int> UpdateAsync(T entity)
        {
            entity.UpdateTime = DateTime.Now;

            var properties = typeof(T).GetProperties()
                .Where(p => p.Name != "Id" && p.Name != "CreateTime" && p.CanWrite);

            var setClauses = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("@Id", entity.Id);

            foreach (var prop in properties)
            {
                var columnName = prop.Name;
                var value = prop.GetValue(entity);

                setClauses.Add($"{columnName} = @{columnName}");
                parameters.Add($"@{columnName}", value);
            }

            var setClause = string.Join(", ", setClauses);
            string sql = $"UPDATE {_tableNameWithSchema} SET {setClause} WHERE Id = @Id";

            return await _sqlHelper.ExecuteAsync(sql, parameters);
        }

        #endregion

        #region ===================== 删除 =====================

        /// <summary> 按 Id 删除（同步） </summary>
        public virtual int Delete(int id)
        {
            string sql = $"DELETE FROM {_tableNameWithSchema} WHERE Id = @Id";
            return _sqlHelper.Execute(sql, new { Id = id });
        }

        /// <summary> 按 Id 删除（异步） </summary>
        public virtual async Task<int> DeleteAsync(int id)
        {
            string sql = $"DELETE FROM {_tableNameWithSchema} WHERE Id = @Id";
            return await _sqlHelper.ExecuteAsync(sql, new { Id = id });
        }

        #endregion

        #region ===================== 扩展查询 =====================

        /// <summary> 检查地址映射表中 DataName 是否重复 </summary>
        /// <param name="stationId">工位 Id</param>
        /// <param name="dataName">数据名称</param>
        /// <returns>true=已存在，false=不存在</returns>
        public async Task<bool> IsDataNameDuplicateAsync(int stationId, string dataName)
        {
            var sql = "SELECT COUNT(1) FROM device_AddressMapping WHERE StationId = @StationId AND DataName = @DataName";
            var count = await _sqlHelper.ExecuteScalarAsync<int>(sql, new { StationId = stationId, DataName = dataName });
            return count > 0;
        }

        /// <summary> 执行自定义 SQL，返回首条或 null </summary>
        public async Task<TResult?> QueryFirstOrDefaultAsync<TResult>(string sql, object? parameters = null)
        {
            return await _sqlHelper.QuerySingleAsync<TResult>(sql, parameters);
        }

        /// <summary> 执行自定义 SQL，返回列表 </summary>
        public async Task<IEnumerable<TResult>?> QueryAsync<TResult>(string sql, object? parameters = null)
        {
            return await _sqlHelper.QueryAsync<TResult>(sql, parameters);
        }

        /// <summary> 执行自定义 SQL，返回单条 </summary>
        public async Task<TResult?> QuerySingleAsync<TResult>(string sql, object? parameters = null)
        {
            return await _sqlHelper.QuerySingleAsync<TResult>(sql, parameters);
        }

        /// <summary> 执行自定义增删改 SQL </summary>
        public async Task<int> ExecuteAsync(string sql, object? parameters = null)
        {
            return await _sqlHelper.ExecuteAsync(sql, parameters);
        }

        /// <summary> 执行自定义标量 SQL，返回单个值 </summary>
        public async Task<TResult?> ExecuteScalarAsync<TResult>(string sql, object? parameters = null)
        {
            return await _sqlHelper.ExecuteScalarAsync<TResult>(sql, parameters);
        }

        #endregion
    }
}
