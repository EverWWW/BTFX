using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using ToolHelper.Database.Abstractions;
using ToolHelper.Database.Configuration;

namespace ToolHelper.Database.Core;

/// <summary>
/// SqlSugar 数据库帮助类实现
/// 基于 SqlSugar ORM，无需编写 SQL 语句
/// </summary>
/// <example>
/// <code>
/// // 创建帮助类
/// var options = Options.Create(new SqliteSugarOptions
/// {
///     DatabasePath = "mydata.db"
/// });
/// var helper = new SqlSugarDbHelper(options);
/// 
/// // 创建表
/// helper.CreateTable&lt;User&gt;();
/// 
/// // 插入数据（无需写SQL）
/// var user = new User { Name = "张三", Age = 25 };
/// helper.Insert(user);
/// 
/// // 查询数据（Lambda表达式）
/// var users = helper.GetList&lt;User&gt;(u => u.Age > 18);
/// 
/// // 分页查询
/// var pagedResult = await helper.GetPageListAsync&lt;User&gt;(
///     u => u.IsActive,
///     pageIndex: 1,
///     pageSize: 10,
///     orderByExpression: u => u.CreateTime,
///     isAsc: false);
/// </code>
/// </example>
public class SqlSugarDbHelper : ISqlSugarDbHelper
{
    private readonly SqlSugarOptions _options;
    private readonly ILogger? _logger;
    private readonly SqlSugarClient _db;
    private bool _disposed;

    /// <inheritdoc/>
    public ISqlSugarClient Db => _db;

    /// <inheritdoc/>
    public DbType DatabaseType => _options.DbType;

    /// <inheritdoc/>
    public bool IsConnected
    {
        get
        {
            try
            {
                return _db.Ado.IsValidConnection();
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// 创建 SqlSugarDbHelper 实例
    /// </summary>
    /// <param name="options">SqlSugar配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
    public SqlSugarDbHelper(IOptions<SqlSugarOptions> options, ILogger<SqlSugarDbHelper>? logger = null)
    {
        _options = options.Value;
        _logger = logger;
        _db = CreateSqlSugarClient();

        // 初始化数据库
        if (_options.InitDatabase && _options.InitEntityTypes?.Length > 0)
        {
            CreateTables(_options.InitEntityTypes);
        }
    }

    /// <summary>
    /// 使用具体配置创建实例
    /// </summary>
    public SqlSugarDbHelper(SqlSugarOptions options, ILogger<SqlSugarDbHelper>? logger = null)
    {
        _options = options;
        _logger = logger;
        _db = CreateSqlSugarClient();

        if (_options.InitDatabase && _options.InitEntityTypes?.Length > 0)
        {
            CreateTables(_options.InitEntityTypes);
        }
    }

    /// <summary>
    /// 创建 SqlSugarClient
    /// </summary>
    private SqlSugarClient CreateSqlSugarClient()
    {
        var config = _options.ToConnectionConfig();
        config.AopEvents = new AopEvents();

        var client = new SqlSugarClient(config);

        // 配置 SQL 日志
        if (_options.EnableSqlLog)
        {
            client.Aop.OnLogExecuting = (sql, pars) =>
            {
                _options.OnLogExecuting?.Invoke(sql, pars);
                _logger?.LogDebug("执行SQL: {Sql}\n参数: {Parameters}",
                    sql,
                    string.Join(", ", pars.Select(p => $"{p.ParameterName}={p.Value}")));
            };

            client.Aop.OnLogExecuted = (sql, pars) =>
            {
                _options.OnLogExecuted?.Invoke(sql, pars);
                _logger?.LogDebug("SQL执行完成，耗时: {Time}ms", client.Ado.SqlExecutionTime.TotalMilliseconds);
            };
        }

        // 配置错误处理
        client.Aop.OnError = ex =>
        {
            _options.OnError?.Invoke(ex);
            _logger?.LogError(ex, "SQL执行错误");
        };

        return client;
    }

    #region 连接管理

    /// <inheritdoc/>
    public bool TestConnection()
    {
        try
        {
            return _db.Ado.IsValidConnection();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "测试连接失败");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            return await Task.Run(() => _db.Ado.IsValidConnection());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "测试连接失败");
            return false;
        }
    }

    #endregion

    #region 查询操作

    /// <inheritdoc/>
    public T? GetById<T>(object id) where T : class, new()
    {
        return _db.Queryable<T>().InSingle(id);
    }

    /// <inheritdoc/>
    public async Task<T?> GetByIdAsync<T>(object id) where T : class, new()
    {
        return await _db.Queryable<T>().InSingleAsync(id);
    }

    /// <inheritdoc/>
    public List<T> GetAll<T>() where T : class, new()
    {
        return _db.Queryable<T>().ToList();
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetAllAsync<T>() where T : class, new()
    {
        return await _db.Queryable<T>().ToListAsync();
    }

    /// <inheritdoc/>
    public List<T> GetList<T>(Expression<Func<T, bool>> whereExpression) where T : class, new()
    {
        return _db.Queryable<T>().Where(whereExpression).ToList();
    }

    /// <inheritdoc/>
    public async Task<List<T>> GetListAsync<T>(Expression<Func<T, bool>> whereExpression) where T : class, new()
    {
        return await _db.Queryable<T>().Where(whereExpression).ToListAsync();
    }

    /// <inheritdoc/>
    public T? GetFirst<T>(Expression<Func<T, bool>> whereExpression) where T : class, new()
    {
        return _db.Queryable<T>().Where(whereExpression).First();
    }

    /// <inheritdoc/>
    public async Task<T?> GetFirstAsync<T>(Expression<Func<T, bool>> whereExpression) where T : class, new()
    {
        return await _db.Queryable<T>().Where(whereExpression).FirstAsync();
    }

    /// <inheritdoc/>
    public List<T> GetPageList<T>(
        Expression<Func<T, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        ref int totalCount,
        Expression<Func<T, object>>? orderByExpression = null,
        bool isAsc = true) where T : class, new()
    {
        var query = _db.Queryable<T>();

        if (whereExpression != null)
        {
            query = query.Where(whereExpression);
        }

        if (orderByExpression != null)
        {
            query = isAsc
                ? query.OrderBy(orderByExpression)
                : query.OrderBy(orderByExpression, OrderByType.Desc);
        }

        return query.ToPageList(pageIndex, pageSize, ref totalCount);
    }

    /// <inheritdoc/>
    public async Task<DbPagedResult<T>> GetPageListAsync<T>(
        Expression<Func<T, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        Expression<Func<T, object>>? orderByExpression = null,
        bool isAsc = true) where T : class, new()
    {
        var query = _db.Queryable<T>();

        if (whereExpression != null)
        {
            query = query.Where(whereExpression);
        }

        if (orderByExpression != null)
        {
            query = isAsc
                ? query.OrderBy(orderByExpression)
                : query.OrderBy(orderByExpression, OrderByType.Desc);
        }

        RefAsync<int> totalCount = 0;
        var items = await query.ToPageListAsync(pageIndex, pageSize, totalCount);

        return new DbPagedResult<T>
        {
            Items = items,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = totalCount.Value
            };
        }

    /// <inheritdoc/>
    public bool Any<T>(Expression<Func<T, bool>> whereExpression) where T : class, new()
    {
        return _db.Queryable<T>().Any(whereExpression);
    }

    /// <inheritdoc/>
    public async Task<bool> AnyAsync<T>(Expression<Func<T, bool>> whereExpression) where T : class, new()
    {
        return await _db.Queryable<T>().AnyAsync(whereExpression);
    }

    /// <inheritdoc/>
    public int Count<T>(Expression<Func<T, bool>>? whereExpression = null) where T : class, new()
    {
        var query = _db.Queryable<T>();
        if (whereExpression != null)
        {
            query = query.Where(whereExpression);
        }
        return query.Count();
    }

    /// <inheritdoc/>
    public async Task<int> CountAsync<T>(Expression<Func<T, bool>>? whereExpression = null) where T : class, new()
    {
        var query = _db.Queryable<T>();
        if (whereExpression != null)
        {
            query = query.Where(whereExpression);
        }
        return await query.CountAsync();
    }

    #endregion

    #region 插入操作

    /// <inheritdoc/>
    public bool Insert<T>(T entity) where T : class, new()
    {
        return _db.Insertable(entity).ExecuteCommand() > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> InsertAsync<T>(T entity) where T : class, new()
    {
        return await _db.Insertable(entity).ExecuteCommandAsync() > 0;
    }

    /// <inheritdoc/>
    public int InsertReturnIdentity<T>(T entity) where T : class, new()
    {
        return _db.Insertable(entity).ExecuteReturnIdentity();
    }

    /// <inheritdoc/>
    public async Task<long> InsertReturnIdentityAsync<T>(T entity) where T : class, new()
    {
        return await _db.Insertable(entity).ExecuteReturnBigIdentityAsync();
    }

    /// <inheritdoc/>
    public T InsertReturnEntity<T>(T entity) where T : class, new()
    {
        return _db.Insertable(entity).ExecuteReturnEntity();
    }

    /// <inheritdoc/>
    public async Task<T> InsertReturnEntityAsync<T>(T entity) where T : class, new()
    {
        return await _db.Insertable(entity).ExecuteReturnEntityAsync();
    }

    /// <inheritdoc/>
    public int InsertRange<T>(List<T> entities) where T : class, new()
    {
        return _db.Insertable(entities).ExecuteCommand();
    }

    /// <inheritdoc/>
    public async Task<int> InsertRangeAsync<T>(List<T> entities) where T : class, new()
    {
        return await _db.Insertable(entities).ExecuteCommandAsync();
    }

    #endregion

    #region 更新操作

    /// <inheritdoc/>
    public bool Update<T>(T entity) where T : class, new()
    {
        return _db.Updateable(entity).ExecuteCommand() > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateAsync<T>(T entity) where T : class, new()
    {
        return await _db.Updateable(entity).ExecuteCommandAsync() > 0;
    }

    /// <inheritdoc/>
    public int UpdateRange<T>(List<T> entities) where T : class, new()
    {
        return _db.Updateable(entities).ExecuteCommand();
    }

    /// <inheritdoc/>
    public async Task<int> UpdateRangeAsync<T>(List<T> entities) where T : class, new()
    {
        return await _db.Updateable(entities).ExecuteCommandAsync();
    }

    /// <inheritdoc/>
    public int Update<T>(
        Expression<Func<T, T>> columns,
        Expression<Func<T, bool>> whereExpression) where T : class, new()
    {
        return _db.Updateable<T>()
            .SetColumns(columns)
            .Where(whereExpression)
            .ExecuteCommand();
    }

    /// <inheritdoc/>
    public async Task<int> UpdateAsync<T>(
        Expression<Func<T, T>> columns,
        Expression<Func<T, bool>> whereExpression) where T : class, new()
    {
        return await _db.Updateable<T>()
            .SetColumns(columns)
            .Where(whereExpression)
            .ExecuteCommandAsync();
    }

    #endregion

    #region 删除操作

    /// <inheritdoc/>
    public bool DeleteById<T>(object id) where T : class, new()
    {
        return _db.Deleteable<T>().In(id).ExecuteCommand() > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteByIdAsync<T>(object id) where T : class, new()
    {
        return await _db.Deleteable<T>().In(id).ExecuteCommandAsync() > 0;
    }

    /// <inheritdoc/>
    public bool Delete<T>(T entity) where T : class, new()
    {
        return _db.Deleteable(entity).ExecuteCommand() > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync<T>(T entity) where T : class, new()
    {
        return await _db.Deleteable(entity).ExecuteCommandAsync() > 0;
    }

    /// <inheritdoc/>
    public int Delete<T>(Expression<Func<T, bool>> whereExpression) where T : class, new()
    {
        return _db.Deleteable<T>().Where(whereExpression).ExecuteCommand();
    }

    /// <inheritdoc/>
    public async Task<int> DeleteAsync<T>(Expression<Func<T, bool>> whereExpression) where T : class, new()
    {
        return await _db.Deleteable<T>().Where(whereExpression).ExecuteCommandAsync();
    }

    /// <inheritdoc/>
    public int DeleteByIds<T>(object[] ids) where T : class, new()
    {
        return _db.Deleteable<T>().In(ids).ExecuteCommand();
    }

    /// <inheritdoc/>
    public async Task<int> DeleteByIdsAsync<T>(object[] ids) where T : class, new()
    {
        return await _db.Deleteable<T>().In(ids).ExecuteCommandAsync();
    }

    #endregion

    #region 事务操作

    /// <inheritdoc/>
    public void BeginTran()
    {
        _db.Ado.BeginTran();
    }

    /// <inheritdoc/>
    public void CommitTran()
    {
        _db.Ado.CommitTran();
    }

    /// <inheritdoc/>
    public void RollbackTran()
    {
        _db.Ado.RollbackTran();
    }

    /// <inheritdoc/>
    public bool ExecuteInTransaction(Action action)
    {
        try
        {
            _db.Ado.BeginTran();
            action();
            _db.Ado.CommitTran();
            return true;
        }
        catch (Exception ex)
        {
            _db.Ado.RollbackTran();
            _logger?.LogError(ex, "事务执行失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public T ExecuteInTransaction<T>(Func<T> func)
    {
        try
        {
            _db.Ado.BeginTran();
            var result = func();
            _db.Ado.CommitTran();
            return result;
        }
        catch (Exception ex)
        {
            _db.Ado.RollbackTran();
            _logger?.LogError(ex, "事务执行失败");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ExecuteInTransactionAsync(Func<Task> action)
    {
        try
        {
            await _db.Ado.BeginTranAsync();
            await action();
            await _db.Ado.CommitTranAsync();
            return true;
        }
        catch (Exception ex)
        {
            await _db.Ado.RollbackTranAsync();
            _logger?.LogError(ex, "事务执行失败");
            throw;
        }
    }

    #endregion

    #region 表操作

    /// <inheritdoc/>
    public void CreateTable<T>() where T : class, new()
    {
        _db.CodeFirst.InitTables<T>();
    }

    /// <inheritdoc/>
    public void CreateTables(params Type[] types)
    {
        _db.CodeFirst.InitTables(types);
    }

    /// <inheritdoc/>
    public void DropTable<T>() where T : class, new()
    {
        var tableName = _db.EntityMaintenance.GetTableName<T>();
        _db.DbMaintenance.DropTable(tableName);
    }

    /// <inheritdoc/>
    public void TruncateTable<T>() where T : class, new()
    {
        var tableName = _db.EntityMaintenance.GetTableName<T>();
        _db.DbMaintenance.TruncateTable(tableName);
    }

    /// <inheritdoc/>
    public bool TableExists<T>() where T : class, new()
    {
        var tableName = _db.EntityMaintenance.GetTableName<T>();
        return _db.DbMaintenance.IsAnyTable(tableName);
    }

    /// <inheritdoc/>
    public bool TableExists(string tableName)
    {
        return _db.DbMaintenance.IsAnyTable(tableName);
    }

    #endregion

    #region 原生SQL

    /// <inheritdoc/>
    public int ExecuteSql(string sql, object? parameters = null)
    {
        if (parameters == null)
        {
            return _db.Ado.ExecuteCommand(sql);
        }
        return _db.Ado.ExecuteCommand(sql, parameters);
    }

    /// <inheritdoc/>
    public async Task<int> ExecuteSqlAsync(string sql, object? parameters = null)
    {
        if (parameters == null)
        {
            return await _db.Ado.ExecuteCommandAsync(sql);
        }
        return await _db.Ado.ExecuteCommandAsync(sql, parameters);
    }

    /// <inheritdoc/>
    public List<T> SqlQuery<T>(string sql, object? parameters = null) where T : class, new()
    {
        if (parameters == null)
        {
            return _db.Ado.SqlQuery<T>(sql);
        }
        return _db.Ado.SqlQuery<T>(sql, parameters);
    }

    /// <inheritdoc/>
    public async Task<List<T>> SqlQueryAsync<T>(string sql, object? parameters = null) where T : class, new()
    {
        if (parameters == null)
        {
            return await _db.Ado.SqlQueryAsync<T>(sql);
        }
        return await _db.Ado.SqlQueryAsync<T>(sql, parameters);
    }

    /// <inheritdoc/>
    public T? SqlQueryScalar<T>(string sql, object? parameters = null)
    {
        object? result;
        if (parameters == null)
        {
            result = _db.Ado.GetScalar(sql);
        }
        else
        {
            result = _db.Ado.GetScalar(sql, parameters);
        }

        if (result == null || result == DBNull.Value)
        {
            return default;
        }
        return (T)Convert.ChangeType(result, typeof(T));
    }

    /// <inheritdoc/>
    public async Task<T?> SqlQueryScalarAsync<T>(string sql, object? parameters = null)
    {
        object? result;
        if (parameters == null)
        {
            result = await _db.Ado.GetScalarAsync(sql);
        }
        else
        {
            result = await _db.Ado.GetScalarAsync(sql, parameters);
        }

        if (result == null || result == DBNull.Value)
        {
            return default;
        }
        return (T)Convert.ChangeType(result, typeof(T));
    }

    #endregion

    #region 高级查询

    /// <inheritdoc/>
    public ISugarQueryable<T> Queryable<T>() where T : class, new()
    {
        return _db.Queryable<T>();
    }

    /// <inheritdoc/>
    public IInsertable<T> Insertable<T>(T entity) where T : class, new()
    {
        return _db.Insertable(entity);
    }

    /// <inheritdoc/>
    public IInsertable<T> Insertable<T>(List<T> entities) where T : class, new()
    {
        return _db.Insertable(entities);
    }

    /// <inheritdoc/>
    public IUpdateable<T> Updateable<T>(T entity) where T : class, new()
    {
        return _db.Updateable(entity);
    }

    /// <inheritdoc/>
    public IDeleteable<T> Deleteable<T>() where T : class, new()
    {
        return _db.Deleteable<T>();
    }

    #endregion

        #region 释放资源

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源（受保护的虚方法，允许子类重写）
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try
                {
                    // 关闭所有连接
                    _db.Close();

                    // 释放 SqlSugar 客户端
                    _db.Dispose();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "释放 SqlSugar 资源时发生错误");
                }
            }

            _disposed = true;
        }

        #endregion
    }
