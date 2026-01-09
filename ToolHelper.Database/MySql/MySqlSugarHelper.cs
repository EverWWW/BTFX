using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using ToolHelper.Database.Abstractions;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Core;

namespace ToolHelper.Database.MySql;

/// <summary>
/// MySQL 数据库帮助类（基于 SqlSugar ORM）
/// 提供 MySQL 数据库的 ORM 操作
/// </summary>
/// <example>
/// <code>
/// // 创建 MySQL 帮助类
/// var options = Options.Create(new MySqlSugarOptions
/// {
///     Server = "localhost",
///     Port = 3306,
///     Database = "mydb",
///     UserId = "root",
///     Password = "password"
/// });
/// 
/// using var db = new MySqlSugarHelper(options);
/// 
/// // 自动创建表
/// db.CreateTable&lt;User&gt;();
/// 
/// // 批量插入（高性能）
/// var users = new List&lt;User&gt; { ... };
/// db.BulkInsert(users);
/// 
/// // 插入或更新（ON DUPLICATE KEY UPDATE）
/// db.InsertOrUpdate(user);
/// </code>
/// </example>
public class MySqlSugarHelper : SqlSugarDbHelper
{
    private readonly MySqlSugarOptions _mysqlOptions;

    /// <summary>
    /// 创建 MySqlSugarHelper 实例
    /// </summary>
    /// <param name="options">MySQL 配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
    public MySqlSugarHelper(IOptions<MySqlSugarOptions> options, ILogger<MySqlSugarHelper>? logger = null)
        : base(CreateOptions(options.Value), logger)
    {
        _mysqlOptions = options.Value;
    }

    /// <summary>
    /// 使用配置创建实例
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public MySqlSugarHelper(MySqlSugarOptions options, ILogger<MySqlSugarHelper>? logger = null)
        : base(CreateOptions(options), logger)
    {
        _mysqlOptions = options;
    }

    /// <summary>
    /// 使用连接字符串创建实例
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="logger">日志记录器</param>
    public MySqlSugarHelper(string connectionString, ILogger<MySqlSugarHelper>? logger = null)
        : base(new MySqlSugarOptions { ConnectionString = connectionString }, logger)
    {
        _mysqlOptions = new MySqlSugarOptions { ConnectionString = connectionString };
    }

    private static SqlSugarOptions CreateOptions(MySqlSugarOptions options)
    {
        options.AutoSetConnectionString();
        return options;
    }

    #region MySQL 特有功能

    /// <summary>
    /// 高性能批量插入
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体集合</param>
    /// <returns>插入行数</returns>
    public int BulkInsert<T>(List<T> entities) where T : class, new()
    {
        return ((SqlSugarClient)Db).Fastest<T>().BulkCopy(entities);
    }

    /// <summary>
    /// 异步高性能批量插入
    /// </summary>
    public async Task<int> BulkInsertAsync<T>(List<T> entities) where T : class, new()
    {
        return await ((SqlSugarClient)Db).Fastest<T>().BulkCopyAsync(entities);
    }

    /// <summary>
    /// 高性能批量更新
    /// </summary>
    public int BulkUpdate<T>(List<T> entities) where T : class, new()
    {
        return ((SqlSugarClient)Db).Fastest<T>().BulkUpdate(entities);
    }

    /// <summary>
    /// 异步高性能批量更新
    /// </summary>
    public async Task<int> BulkUpdateAsync<T>(List<T> entities) where T : class, new()
    {
        return await ((SqlSugarClient)Db).Fastest<T>().BulkUpdateAsync(entities);
    }

    /// <summary>
    /// 批量合并（插入或更新，ON DUPLICATE KEY UPDATE）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体集合</param>
    /// <returns>受影响行数</returns>
    public int BulkMerge<T>(List<T> entities) where T : class, new()
    {
        return ((SqlSugarClient)Db).Fastest<T>().BulkMerge(entities);
    }

    /// <summary>
    /// 异步批量合并
    /// </summary>
    public async Task<int> BulkMergeAsync<T>(List<T> entities) where T : class, new()
    {
        return await ((SqlSugarClient)Db).Fastest<T>().BulkMergeAsync(entities);
    }

    /// <summary>
    /// 插入或更新（ON DUPLICATE KEY UPDATE）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <returns>是否成功</returns>
    public bool InsertOrUpdate<T>(T entity) where T : class, new()
    {
        return ((SqlSugarClient)Db).Storageable(entity)
            .ExecuteCommand() > 0;
    }

    /// <summary>
    /// 异步插入或更新
    /// </summary>
    public async Task<bool> InsertOrUpdateAsync<T>(T entity) where T : class, new()
    {
        return await ((SqlSugarClient)Db).Storageable(entity)
            .ExecuteCommandAsync() > 0;
    }

    /// <summary>
    /// 批量插入或更新
    /// </summary>
    public int InsertOrUpdateRange<T>(List<T> entities) where T : class, new()
    {
        return ((SqlSugarClient)Db).Storageable(entities)
            .ExecuteCommand();
    }

    /// <summary>
    /// 异步批量插入或更新
    /// </summary>
    public async Task<int> InsertOrUpdateRangeAsync<T>(List<T> entities) where T : class, new()
    {
        return await ((SqlSugarClient)Db).Storageable(entities)
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 执行存储过程（无返回值）
    /// </summary>
    /// <param name="procedureName">存储过程名称</param>
    /// <param name="parameters">参数</param>
    /// <returns>受影响行数</returns>
    public int ExecuteStoredProcedure(string procedureName, object? parameters = null)
    {
        if (parameters == null)
        {
            return Db.Ado.UseStoredProcedure().ExecuteCommand(procedureName);
        }
        return Db.Ado.UseStoredProcedure().ExecuteCommand(procedureName, parameters);
    }

    /// <summary>
    /// 执行存储过程并返回结果
    /// </summary>
    public List<T> ExecuteStoredProcedure<T>(string procedureName, object? parameters = null) where T : class, new()
    {
        if (parameters == null)
        {
            return Db.Ado.UseStoredProcedure().SqlQuery<T>(procedureName);
        }
        return Db.Ado.UseStoredProcedure().SqlQuery<T>(procedureName, parameters);
    }

    /// <summary>
    /// 获取数据库信息
    /// </summary>
    /// <returns>数据库信息</returns>
    public MySqlDatabaseInfo GetDatabaseInfo()
    {
        var sql = @"
            SELECT 
                DATABASE() AS DatabaseName,
                SUM(data_length) / 1024 / 1024 AS DataSizeMB,
                SUM(index_length) / 1024 / 1024 AS IndexSizeMB,
                SUM(data_length + index_length) / 1024 / 1024 AS TotalSizeMB,
                COUNT(*) AS TableCount
            FROM information_schema.TABLES 
            WHERE TABLE_SCHEMA = DATABASE()";

        return Db.Ado.SqlQuerySingle<MySqlDatabaseInfo>(sql);
    }

    /// <summary>
    /// 获取 MySQL 服务器版本
    /// </summary>
    /// <returns>版本字符串</returns>
    public string GetServerVersion()
    {
        return Db.Ado.GetScalar("SELECT VERSION()")?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 获取当前连接数
    /// </summary>
    /// <returns>连接数</returns>
    public int GetConnectionCount()
    {
        return Convert.ToInt32(Db.Ado.GetScalar("SELECT COUNT(*) FROM information_schema.PROCESSLIST"));
    }

    /// <summary>
    /// 获取所有表名
    /// </summary>
    /// <returns>表名列表</returns>
    public List<string> GetAllTableNames()
    {
        return Db.DbMaintenance.GetTableInfoList()
            .Select(t => t.Name)
            .ToList();
    }

    /// <summary>
    /// 优化表
    /// </summary>
    /// <param name="tableName">表名</param>
    public void OptimizeTable(string tableName)
    {
        ExecuteSql($"OPTIMIZE TABLE `{tableName}`");
    }

    /// <summary>
    /// 分析表
    /// </summary>
    /// <param name="tableName">表名</param>
    public void AnalyzeTable(string tableName)
    {
        ExecuteSql($"ANALYZE TABLE `{tableName}`");
    }

    /// <summary>
    /// 检查表
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <returns>检查结果</returns>
    public List<MySqlCheckResult> CheckTable(string tableName)
    {
        return Db.Ado.SqlQuery<MySqlCheckResult>($"CHECK TABLE `{tableName}`");
    }

    /// <summary>
    /// 截断表
    /// </summary>
    /// <param name="tableName">表名</param>
    public void TruncateTable(string tableName)
    {
        Db.DbMaintenance.TruncateTable(tableName);
    }

        #endregion
    }
