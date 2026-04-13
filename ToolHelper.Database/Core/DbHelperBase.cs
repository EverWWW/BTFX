using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using ToolHelper.Database.Abstractions;
using ToolHelper.Database.Configuration;

namespace ToolHelper.Database.Core;

/// <summary>
/// 数据库帮助类基类
/// 提供通用数据库操作实现
/// </summary>
public abstract class DbHelperBase : IDbHelper
{
    private readonly DatabaseOptions _options;
    private readonly ILogger? _logger;
    private DbConnection? _connection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;

    // 类型映射缓存
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _typePropertyCache = new();

    /// <inheritdoc/>
    public abstract DatabaseType DatabaseType { get; }

    /// <inheritdoc/>
    public string ConnectionString { get; }

    /// <inheritdoc/>
    public bool IsConnected => _connection?.State == ConnectionState.Open;

    /// <summary>
    /// 创建DbHelperBase实例
    /// </summary>
    /// <param name="options">数据库配置</param>
    /// <param name="logger">日志记录器</param>
    protected DbHelperBase(DatabaseOptions options, ILogger? logger = null)
    {
        _options = options;
        _logger = logger;
        ConnectionString = options.ConnectionString;
    }

    /// <summary>
    /// 创建数据库连接
    /// </summary>
    protected abstract DbConnection CreateConnection();

    /// <summary>
    /// 获取参数前缀
    /// </summary>
    protected abstract string ParameterPrefix { get; }

    #region 连接管理

    /// <inheritdoc/>
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection == null)
            {
                _connection = CreateConnection();
            }

            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync(cancellationToken);
                LogDebug("数据库连接已打开");
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task CloseAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_connection != null && _connection.State != ConnectionState.Closed)
            {
                await _connection.CloseAsync();
                LogDebug("数据库连接已关闭");
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            LogError("测试连接失败", ex);
            return false;
        }
    }

    /// <summary>
    /// 获取或创建连接
    /// </summary>
    protected async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        await OpenAsync(cancellationToken);
        return _connection!;
    }

    #endregion

    #region 执行命令

    /// <inheritdoc/>
    public async Task<int> ExecuteNonQueryAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        using var command = CreateCommand(connection, sql, parameters);
        
        LogSql(sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        using var command = CreateCommand(connection, sql, parameters);

        LogSql(sql, parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        if (result == null || result == DBNull.Value)
        {
            return default;
        }

        return (T)Convert.ChangeType(result, typeof(T));
    }

    /// <inheritdoc/>
    public async Task<T?> QueryFirstOrDefaultAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        var connection = await GetConnectionAsync(cancellationToken);
        using var command = CreateCommand(connection, sql, parameters);

        LogSql(sql, parameters);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToEntity<T>(reader);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<T>> QueryAsync<T>(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        var connection = await GetConnectionAsync(cancellationToken);
        using var command = CreateCommand(connection, sql, parameters);

        LogSql(sql, parameters);
        var results = new List<T>();

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapToEntity<T>(reader));
        }

        return results;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<T> QueryStreamAsync<T>(
        string sql,
        object? parameters = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, new()
    {
        var connection = await GetConnectionAsync(cancellationToken);
        using var command = CreateCommand(connection, sql, parameters);

        LogSql(sql, parameters);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            yield return MapToEntity<T>(reader);
        }
    }

    /// <inheritdoc/>
    public async Task<DataTable> QueryDataTableAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        using var command = CreateCommand(connection, sql, parameters);

        LogSql(sql, parameters);
        var dataTable = new DataTable();

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        dataTable.Load(reader);

        return dataTable;
    }

    #endregion

    #region 事务管理

    /// <inheritdoc/>
    public async Task<IDbTransaction> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        return await ((DbConnection)connection).BeginTransactionAsync(isolationLevel, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ExecuteInTransactionAsync(
        Func<IDbTransaction, Task> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            await action(transaction);
            await transaction.CommitAsync(cancellationToken);
            LogDebug("事务提交成功");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            LogDebug("事务已回滚");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<IDbTransaction, Task<T>> action,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            var result = await action(transaction);
            await transaction.CommitAsync(cancellationToken);
            LogDebug("事务提交成功");
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            LogDebug("事务已回滚");
            throw;
        }
    }

    #endregion

    #region 批量操作

    /// <inheritdoc/>
    public virtual async Task<int> BulkInsertAsync<T>(
        string tableName,
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default) where T : class
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0) return 0;

        var properties = GetCachedProperties(typeof(T));
        var columns = string.Join(", ", properties.Select(p => p.Name));
        var parameters = string.Join(", ", properties.Select(p => $"{ParameterPrefix}{p.Name}"));

        var sql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";
        var totalInserted = 0;

        var connection = await GetConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var batch in entityList.Chunk(_options.BatchSize))
            {
                foreach (var entity in batch)
                {
                    using var command = CreateCommand(connection, sql, entity);
                    command.Transaction = (DbTransaction)transaction;
                    totalInserted += await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            LogDebug($"批量插入完成，共插入 {totalInserted} 条记录");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return totalInserted;
    }

    /// <inheritdoc/>
    public virtual async Task<int> BulkUpdateAsync<T>(
        string tableName,
        IEnumerable<T> entities,
        string[] keyColumns,
        CancellationToken cancellationToken = default) where T : class
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0) return 0;

        var properties = GetCachedProperties(typeof(T));
        var keySet = new HashSet<string>(keyColumns, StringComparer.OrdinalIgnoreCase);
        
        var setClause = string.Join(", ", 
            properties.Where(p => !keySet.Contains(p.Name))
                     .Select(p => $"{p.Name} = {ParameterPrefix}{p.Name}"));
        
        var whereClause = string.Join(" AND ", 
            keyColumns.Select(k => $"{k} = {ParameterPrefix}{k}"));

        var sql = $"UPDATE {tableName} SET {setClause} WHERE {whereClause}";
        var totalUpdated = 0;

        var connection = await GetConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var entity in entityList)
            {
                using var command = CreateCommand(connection, sql, entity);
                command.Transaction = (DbTransaction)transaction;
                totalUpdated += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            LogDebug($"批量更新完成，共更新 {totalUpdated} 条记录");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return totalUpdated;
    }

    /// <inheritdoc/>
    public virtual async Task<int> BulkDeleteAsync<T>(
        string tableName,
        IEnumerable<T> entities,
        string[] keyColumns,
        CancellationToken cancellationToken = default) where T : class
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0) return 0;

        var whereClause = string.Join(" AND ", 
            keyColumns.Select(k => $"{k} = {ParameterPrefix}{k}"));

        var sql = $"DELETE FROM {tableName} WHERE {whereClause}";
        var totalDeleted = 0;

        var connection = await GetConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var entity in entityList)
            {
                using var command = CreateCommand(connection, sql, entity);
                command.Transaction = (DbTransaction)transaction;
                totalDeleted += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            LogDebug($"批量删除完成，共删除 {totalDeleted} 条记录");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return totalDeleted;
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 创建命令对象
    /// </summary>
    protected DbCommand CreateCommand(DbConnection connection, string sql, object? parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = _options.CommandTimeout;

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        return command;
    }

    /// <summary>
    /// 添加参数
    /// </summary>
    protected virtual void AddParameters(DbCommand command, object parameters)
    {
        var properties = GetCachedProperties(parameters.GetType());

        foreach (var property in properties)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"{ParameterPrefix}{property.Name}";
            parameter.Value = property.GetValue(parameters) ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    /// <summary>
    /// 映射到实体
    /// </summary>
    protected T MapToEntity<T>(DbDataReader reader) where T : class, new()
    {
        var entity = new T();
        var properties = GetCachedProperties(typeof(T));
        var columnNames = GetColumnNames(reader);

        foreach (var property in properties)
        {
            if (!columnNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                continue;

            var ordinal = reader.GetOrdinal(property.Name);
            if (reader.IsDBNull(ordinal))
                continue;

            var value = reader.GetValue(ordinal);
            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (targetType.IsEnum)
            {
                value = Enum.ToObject(targetType, value);
            }
            else if (targetType == typeof(Guid) && value is string stringValue)
            {
                value = Guid.Parse(stringValue);
            }
            else
            {
                value = Convert.ChangeType(value, targetType);
            }

            property.SetValue(entity, value);
        }

        return entity;
    }

    /// <summary>
    /// 获取列名
    /// </summary>
    private static HashSet<string> GetColumnNames(DbDataReader reader)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            names.Add(reader.GetName(i));
        }
        return names;
    }

    /// <summary>
    /// 获取缓存的属性
    /// </summary>
    protected static PropertyInfo[] GetCachedProperties(Type type)
    {
        return _typePropertyCache.GetOrAdd(type, t =>
            t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
             .Where(p => p.CanRead && p.CanWrite)
             .ToArray());
    }

    #endregion

    #region 日志

    /// <summary>
    /// 记录SQL日志
    /// </summary>
    protected void LogSql(string sql, object? parameters)
    {
        if (_options.EnableLogging && _logger != null)
        {
            _logger.LogDebug("执行SQL: {Sql}, 参数: {Parameters}", sql, parameters);
        }
    }

    /// <summary>
    /// 记录调试日志
    /// </summary>
    protected void LogDebug(string message)
    {
        if (_options.EnableLogging && _logger != null)
        {
            _logger.LogDebug(message);
        }
    }

    /// <summary>
    /// 记录错误日志
    /// </summary>
    protected void LogError(string message, Exception ex)
    {
        _logger?.LogError(ex, message);
    }

    #endregion

    #region 释放资源

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _connection?.Dispose();
            _connectionLock.Dispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// 异步释放资源
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_disposed) return;

        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }

    #endregion
}
