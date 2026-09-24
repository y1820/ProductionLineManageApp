using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Dapper;

namespace ProductionLineManage.Infrastructure.Data.Repository
{
    /// <summary>
    /// SQL Server 数据库帮助类（基于 Dapper）：封装连接、增删改查、标量查询与事务，支持依赖注入。
    /// </summary>
    public class SQLHelper
    {
        #region ===================== 字段与构造 =====================

        /// <summary> 数据库连接字符串 </summary>
        private readonly string _connectionString;

        /// <summary> 从配置读取 SCADADatabase 连接字符串 </summary>
        /// <param name="configuration">配置对象（appsettings.json）</param>
        public SQLHelper(IConfiguration configuration)
        {
            // 从 appsettings.json 读取连接字符串（可选：SCADADatabaseTest / SCADADatabaseReducer2 / SCADADatabaseRLD19145）
            _connectionString = configuration.GetConnectionString("SCADADatabase")
                ?? throw new InvalidOperationException("未找到连接字符串 SCADADatabase");
        }

        #endregion

        #region ===================== 连接管理 =====================

        /// <summary> 创建并返回 SqlConnection 实例 </summary>
        private SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString); // 每次操作独立连接，using 自动释放
        }

        #endregion

        #region ===================== 增删改 =====================

        /// <summary> 执行增删改（同步） </summary>
        /// <param name="sql">SQL 语句（建议使用参数化）</param>
        /// <param name="parameters">参数对象</param>
        /// <returns>受影响的行数</returns>
        public int Execute(string sql, object? parameters = null)
        {
            using var connection = CreateConnection(); // 打开连接并执行
            return connection.Execute(sql, parameters);
        }

        /// <summary> 执行增删改（异步） </summary>
        /// <param name="sql">SQL 语句（建议使用参数化）</param>
        /// <param name="parameters">参数对象</param>
        /// <returns>受影响的行数</returns>
        public async Task<int> ExecuteAsync(string sql, object? parameters = null)
        {
            using var connection = CreateConnection(); // 异步打开连接并执行
            return await connection.ExecuteAsync(sql, parameters);
        }

        #endregion

        #region ===================== 查询 =====================

        /// <summary> 查询单个实体（同步），未找到返回 null </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="sql">SQL 语句</param>
        /// <param name="parameters">参数对象</param>
        public T? QuerySingle<T>(string sql, object? parameters = null)
        {
            using var connection = CreateConnection();
            return connection.QueryFirstOrDefault<T>(sql, parameters); // 取首条或默认
        }

        /// <summary> 查询单个实体（异步），未找到返回 null </summary>
        public async Task<T?> QuerySingleAsync<T>(string sql, object? parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<T>(sql, parameters); // 异步取首条或默认
        }

        /// <summary> 查询实体列表（同步） </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="sql">SQL 语句</param>
        /// <param name="parameters">参数对象</param>
        /// <returns>实体集合</returns>
        public IEnumerable<T> Query<T>(string sql, object? parameters = null)
        {
            using var connection = CreateConnection();
            return connection.Query<T>(sql, parameters); // 映射为强类型集合
        }

        /// <summary> 查询实体列表（异步），返回 IEnumerable&lt;T&gt; </summary>
        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.QueryAsync<T>(sql, parameters); // 异步映射为强类型集合
        }

        #endregion

        #region ===================== 标量查询 =====================

        /// <summary> 执行标量查询（同步），返回单个值 </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="sql">SQL 语句</param>
        /// <param name="parameters">参数对象</param>
        public T? ExecuteScalar<T>(string sql, object? parameters = null)
        {
            using var connection = CreateConnection();
            return connection.ExecuteScalar<T>(sql, parameters); // 如 COUNT、SCOPE_IDENTITY
        }

        /// <summary> 执行标量查询（异步） </summary>
        public async Task<T?> ExecuteScalarAsync<T>(string sql, object? parameters = null)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<T>(sql, parameters);
        }

        #endregion

        #region ===================== 事务 =====================

        /// <summary> 在事务中执行操作（同步），成功提交，异常回滚 </summary>
        /// <param name="action">事务内的操作</param>
        /// <returns>是否成功</returns>
        public bool ExecuteInTransaction(Action<IDbConnection, IDbTransaction> action)
        {
            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction(); // 开启事务
            try
            {
                action(connection, transaction); // 执行业务逻辑
                transaction.Commit(); // 全部成功则提交
                return true;
            }
            catch
            {
                transaction.Rollback(); // 异常时回滚
                throw;
            }
        }

        /// <summary> 在事务中执行操作（异步），成功提交，异常回滚 </summary>
        public async Task<bool> ExecuteInTransactionAsync(Func<IDbConnection, IDbTransaction, Task> action)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                await action(connection, transaction);
                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        #endregion
    }
}
