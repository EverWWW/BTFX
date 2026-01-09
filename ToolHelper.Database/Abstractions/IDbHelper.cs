using System.Data;
using System.Data.Common;

namespace ToolHelper.Database.Abstractions;

/// <summary>
/// 数据库帮助类基础接口
/// 定义所有数据库操作的通用方法
/// </summary>
public interface IDbHelper : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// 数据库类型
    /// </summary>
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// 连接字符串
    /// </summary>
    string ConnectionString { get; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    #region 连接管理

    /// <summary>
    /// 打开数据库连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 关闭数据库连接
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// 测试数据库连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接是否成功</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    #endregion

    #region 执行命令

    /// <summary>
    /// 执行非查询命令（INSERT, UPDATE, DELETE）
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>受影响的行数</returns>
    Task<int> ExecuteNonQueryAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行标量查询
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果</returns>
    Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询单条记录
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体对象</returns>
    Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// 查询多条记录
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体集合</returns>
    Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// 流式查询（适用于大数据量）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>异步枚举</returns>
    IAsyncEnumerable<T> QueryStreamAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default) where T : class, new();

    /// <summary>
    /// 执行查询返回DataTable
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>DataTable</returns>
    Task<DataTable> QueryDataTableAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    #endregion

    #region 事务管理

    /// <summary>
    /// 开始事务
    /// </summary>
    /// <param name="isolationLevel">隔离级别</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>事务对象</returns>
    Task<IDbTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在事务中执行操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <param name="isolationLevel">隔离级别</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ExecuteInTransactionAsync(
        Func<IDbTransaction, Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在事务中执行操作并返回结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="action">要执行的操作</param>
    /// <param name="isolationLevel">隔离级别</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    Task<T> ExecuteInTransactionAsync<T>(
        Func<IDbTransaction, Task<T>> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    #endregion

    #region 批量操作

    /// <summary>
    /// 批量插入
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="tableName">表名</param>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>插入的行数</returns>
    Task<int> BulkInsertAsync<T>(
        string tableName,
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 批量更新
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="tableName">表名</param>
    /// <param name="entities">实体集合</param>
    /// <param name="keyColumns">主键列</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新的行数</returns>
    Task<int> BulkUpdateAsync<T>(
        string tableName,
        IEnumerable<T> entities,
        string[] keyColumns,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 批量删除
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="tableName">表名</param>
    /// <param name="entities">实体集合</param>
    /// <param name="keyColumns">主键列</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除的行数</returns>
    Task<int> BulkDeleteAsync<T>(
        string tableName,
        IEnumerable<T> entities,
        string[] keyColumns,
        CancellationToken cancellationToken = default) where T : class;

    #endregion
}

/// <summary>
/// 数据库类型枚举
/// </summary>
public enum DatabaseType
{
    /// <summary>SQLite数据库</summary>
    Sqlite,
    /// <summary>SQL Server数据库</summary>
    SqlServer,
    /// <summary>MySQL数据库</summary>
    MySql
}
