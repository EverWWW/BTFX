using System.Data.Common;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolHelper.Database.Abstractions;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Core;

namespace ToolHelper.Database.Sqlite;

/// <summary>
/// SQLite 数据库帮助类
/// 提供SQLite数据库的操作封装
/// </summary>
/// <example>
/// <code>
/// // 创建SQLite帮助类
/// var options = Options.Create(new SqliteOptions
/// {
///     DatabasePath = "mydata.db",
///     JournalMode = SqliteJournalMode.Wal
/// });
/// 
/// using var helper = new SqliteHelper(options);
/// 
/// // 创建表
/// await helper.ExecuteNonQueryAsync(@"
///     CREATE TABLE IF NOT EXISTS Users (
///         Id INTEGER PRIMARY KEY AUTOINCREMENT,
///         Name TEXT NOT NULL,
///         Email TEXT UNIQUE
///     )");
/// 
/// // 插入数据
/// await helper.ExecuteNonQueryAsync(
///     "INSERT INTO Users (Name, Email) VALUES (@Name, @Email)",
///     new { Name = "张三", Email = "zhang@example.com" });
/// 
/// // 查询数据
/// var users = await helper.QueryAsync&lt;User&gt;("SELECT * FROM Users");
/// </code>
/// </example>
public class SqliteHelper : DbHelperBase
{
    private readonly SqliteOptions _sqliteOptions;

    /// <inheritdoc/>
    public override DatabaseType DatabaseType => DatabaseType.Sqlite;

    /// <inheritdoc/>
    protected override string ParameterPrefix => "@";

    /// <summary>
    /// 创建 SqliteHelper 实例
    /// </summary>
    /// <param name="options">SQLite配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
    public SqliteHelper(IOptions<SqliteOptions> options, ILogger<SqliteHelper>? logger = null)
        : base(CreateOptionsWithConnectionString(options.Value), logger)
    {
        _sqliteOptions = options.Value;
    }

    /// <summary>
    /// 使用连接字符串创建 SqliteHelper 实例
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="logger">日志记录器（可选）</param>
    public SqliteHelper(string connectionString, ILogger<SqliteHelper>? logger = null)
        : base(new SqliteOptions { ConnectionString = connectionString }, logger)
    {
        _sqliteOptions = new SqliteOptions { ConnectionString = connectionString };
    }

    private static SqliteOptions CreateOptionsWithConnectionString(SqliteOptions options)
    {
        if (string.IsNullOrEmpty(options.ConnectionString))
        {
            options.ConnectionString = options.BuildConnectionString();
        }
        return options;
    }

    /// <inheritdoc/>
    protected override DbConnection CreateConnection()
    {
        return new SqliteConnection(ConnectionString);
    }

    /// <summary>
    /// 初始化数据库（设置PRAGMA）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await OpenAsync(cancellationToken);

        // 设置日志模式
        await ExecuteNonQueryAsync(
            $"PRAGMA journal_mode = {_sqliteOptions.JournalMode};",
            cancellationToken: cancellationToken);

        // 设置同步模式
        await ExecuteNonQueryAsync(
            $"PRAGMA synchronous = {_sqliteOptions.SynchronousMode};",
            cancellationToken: cancellationToken);

        // 设置缓存大小
        await ExecuteNonQueryAsync(
            $"PRAGMA cache_size = {_sqliteOptions.CacheSize};",
            cancellationToken: cancellationToken);

        // 设置外键约束
        await ExecuteNonQueryAsync(
            $"PRAGMA foreign_keys = {(_sqliteOptions.ForeignKeys ? "ON" : "OFF")};",
            cancellationToken: cancellationToken);
    }

    #region SQLite特有功能

    /// <summary>
    /// 获取最后插入的行ID
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最后插入的行ID</returns>
    public async Task<long> GetLastInsertRowIdAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteScalarAsync<long>("SELECT last_insert_rowid();", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 插入数据并返回自增ID
    /// </summary>
    /// <param name="sql">INSERT SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>自增ID</returns>
    public async Task<long> InsertAndGetIdAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        return await GetLastInsertRowIdAsync(cancellationToken);
    }

    /// <summary>
    /// 执行VACUUM操作（整理数据库）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task VacuumAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync("VACUUM;", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 检查表是否存在
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@TableName;",
            new { TableName = tableName },
            cancellationToken);

        return result > 0;
    }

    /// <summary>
    /// 获取所有表名
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表名列表</returns>
    public async Task<IEnumerable<string>> GetTableNamesAsync(CancellationToken cancellationToken = default)
    {
        var tables = await QueryAsync<TableInfo>(
            "SELECT name AS Name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;",
            cancellationToken: cancellationToken);

        return tables.Select(t => t.Name);
    }

    /// <summary>
    /// 获取表结构信息
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>列信息列表</returns>
    public async Task<IEnumerable<SqliteColumnInfo>> GetTableSchemaAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync<SqliteColumnInfo>(
            $"PRAGMA table_info({tableName});",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 创建索引
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="indexName">索引名</param>
    /// <param name="columns">列名</param>
    /// <param name="isUnique">是否唯一索引</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task CreateIndexAsync(
        string tableName,
        string indexName,
        string[] columns,
        bool isUnique = false,
        CancellationToken cancellationToken = default)
    {
        var uniqueKeyword = isUnique ? "UNIQUE " : "";
        var columnList = string.Join(", ", columns);
        
        await ExecuteNonQueryAsync(
            $"CREATE {uniqueKeyword}INDEX IF NOT EXISTS {indexName} ON {tableName} ({columnList});",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 删除索引
    /// </summary>
    /// <param name="indexName">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task DropIndexAsync(string indexName, CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync($"DROP INDEX IF EXISTS {indexName};", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 备份数据库到文件
    /// </summary>
    /// <param name="backupPath">备份文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task BackupToFileAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        await OpenAsync(cancellationToken);
        
        using var backupConnection = new SqliteConnection($"Data Source={backupPath}");
        await backupConnection.OpenAsync(cancellationToken);

        var sourceConnection = (SqliteConnection)await GetConnectionAsync(cancellationToken);
        sourceConnection.BackupDatabase(backupConnection);
    }

    /// <summary>
    /// 从文件恢复数据库
    /// </summary>
    /// <param name="backupPath">备份文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task RestoreFromFileAsync(string backupPath, CancellationToken cancellationToken = default)
    {
        await CloseAsync();

        using var backupConnection = new SqliteConnection($"Data Source={backupPath}");
        await backupConnection.OpenAsync(cancellationToken);

        using var targetConnection = new SqliteConnection(ConnectionString);
        await targetConnection.OpenAsync(cancellationToken);

        backupConnection.BackupDatabase(targetConnection);
    }

    /// <summary>
    /// 获取数据库大小（字节）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据库大小</returns>
    public async Task<long> GetDatabaseSizeAsync(CancellationToken cancellationToken = default)
    {
        var pageCount = await ExecuteScalarAsync<long>("PRAGMA page_count;", cancellationToken: cancellationToken);
        var pageSize = await ExecuteScalarAsync<long>("PRAGMA page_size;", cancellationToken: cancellationToken);
        return pageCount * pageSize;
    }

    /// <summary>
    /// 执行完整性检查
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检查结果</returns>
    public async Task<IEnumerable<string>> IntegrityCheckAsync(CancellationToken cancellationToken = default)
    {
        var results = await QueryAsync<IntegrityResult>(
            "PRAGMA integrity_check;",
            cancellationToken: cancellationToken);

        return results.Select(r => r.integrity_check);
    }

    /// <summary>
    /// 批量插入优化（使用事务和预编译语句）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="tableName">表名</param>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>插入行数</returns>
    public override async Task<int> BulkInsertAsync<T>(
        string tableName,
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0) return 0;

        var properties = GetCachedProperties(typeof(T));
        var columns = string.Join(", ", properties.Select(p => p.Name));
        var parameters = string.Join(", ", properties.Select(p => $"@{p.Name}"));

        var sql = $"INSERT INTO {tableName} ({columns}) VALUES ({parameters})";
        var totalInserted = 0;

        var connection = (SqliteConnection)await GetConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = (SqliteTransaction)transaction;

            // 预创建参数
            var parameterObjects = properties.Select(p =>
            {
                var param = command.CreateParameter();
                param.ParameterName = $"@{p.Name}";
                command.Parameters.Add(param);
                return (Property: p, Parameter: param);
            }).ToList();

            foreach (var entity in entityList)
            {
                foreach (var (property, parameter) in parameterObjects)
                {
                    parameter.Value = property.GetValue(entity) ?? DBNull.Value;
                }

                totalInserted += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return totalInserted;
    }

    #endregion
}

/// <summary>
/// 表信息
/// </summary>
internal class TableInfo
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// SQLite列信息
/// </summary>
public class SqliteColumnInfo
{
    /// <summary>列ID</summary>
    public int cid { get; set; }
    /// <summary>列名</summary>
    public string name { get; set; } = string.Empty;
    /// <summary>数据类型</summary>
    public string type { get; set; } = string.Empty;
    /// <summary>是否非空</summary>
    public int notnull { get; set; }
    /// <summary>默认值</summary>
    public string? dflt_value { get; set; }
    /// <summary>是否主键</summary>
    public int pk { get; set; }
}

/// <summary>
/// 完整性检查结果
/// </summary>
internal class IntegrityResult
{
    public string integrity_check { get; set; } = string.Empty;
}
