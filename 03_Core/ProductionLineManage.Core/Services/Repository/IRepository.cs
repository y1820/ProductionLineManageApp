namespace ProductionLineManage.Core.Services.RepositoryGrop
{
    /// <summary> 数据库增删改查操作接口 </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public interface IRepository<T> where T : class
    {
        #region ===================== 基础 CRUD =====================

        /// <summary> 获取所有数据 </summary>
        IEnumerable<T> GetAll();
        Task<IEnumerable<T>> GetAllAsync();

        /// <summary> 根据 ID 获取 </summary>
        T? GetById(int id);
        Task<T?> GetByIdAsync(int id);

        /// <summary> 插入数据，返回 Id </summary>
        int Insert(T entity);
        Task<int> InsertAsync(T entity);

        /// <summary> 更新数据，返回受影响的行数 </summary>
        int Update(T entity);
        Task<int> UpdateAsync(T entity);

        /// <summary> 删除数据，返回受影响的行数 </summary>
        int Delete(int id);
        Task<int> DeleteAsync(int id);

        /// <summary> 检查是否存在 </summary>
        bool Exists(int id);
        Task<bool> ExistsAsync(int id);

        #endregion

        #region ===================== 业务校验 =====================

        /// <summary> 检查工位下 DataName 是否重复 </summary>
        Task<bool> IsDataNameDuplicateAsync(int stationId, string dataName);

        #endregion

        #region ===================== 原生 SQL 查询 =====================

        /// <summary> 执行 SQL 查询并返回第一条记录，无记录时返回 null </summary>
        Task<TResult?> QueryFirstOrDefaultAsync<TResult>(string sql, object? parameters = null);

        /// <summary> 执行 SQL 查询并返回多条记录 </summary>
        Task<IEnumerable<TResult>?> QueryAsync<TResult>(string sql, object? parameters = null);

        /// <summary> 执行 SQL 查询并返回单条记录 </summary>
        Task<TResult?> QuerySingleAsync<TResult>(string sql, object? parameters = null);

        /// <summary> 执行非查询 SQL，返回受影响行数 </summary>
        Task<int> ExecuteAsync(string sql, object? parameters = null);

        /// <summary> 执行标量查询 </summary>
        Task<TResult?> ExecuteScalarAsync<TResult>(string sql, object? parameters = null);

        #endregion
    }
}
