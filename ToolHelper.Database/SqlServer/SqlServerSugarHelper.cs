using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using ToolHelper.Database.Abstractions;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Core;

namespace ToolHelper.Database.SqlServer;

/// <summary>
/// SQL Server 数据库帮助类（基于 SqlSugar ORM）
/// 提供 SQL Server 数据库的 ORM 操作
/// </summary>
/// <example>
/// <code>
/// // 创建 SQL Server 帮助类
/// var options = Options.Create(new SqlServerSugarOptions
/// {
///     Server = "localhost",
///     Database = "MyDatabase",
///     UserId = "sa",
///     Password = "YourPassword"
/// });
/// 
/// using var db = new SqlServerSugarHelper(options);
/// 
/// // 自动创建表
/// db.CreateTable&lt;User&gt;();
/// 
/// // 批量插入（高性能）
/// var users = new List&lt;User&gt; { ... };
/// db.BulkInsert(users);
/// 
/// // 存储过程调用
/// var result = db.ExecuteStoredProcedure&lt;User&gt;("sp_GetUsers", new { Age = 18 });
/// </code>
/// </example>
public class SqlServerSugarHelper : SqlSugarDbHelper
{
    private readonly SqlServerSugarOptions _sqlServerOptions;

    /// <summary>
    /// 创建 SqlServerSugarHelper 实例
    /// </summary>
    /// <param name="options">SQL Server 配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
    public SqlServerSugarHelper(IOptions<SqlServerSugarOptions> options, ILogger<SqlServerSugarHelper>? logger = null)
        : base(CreateOptions(options.Value), logger)
    {
        _sqlServerOptions = options.Value;
    }

    /// <summary>
    /// 使用配置创建实例
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public SqlServerSugarHelper(SqlServerSugarOptions options, ILogger<SqlServerSugarHelper>? logger = null)
        : base(CreateOptions(options), logger)
    {
        _sqlServerOptions = options;
    }

    /// <summary>
    /// 使用连接字符串创建实例
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="logger">日志记录器</param>
    public SqlServerSugarHelper(string connectionString, ILogger<SqlServerSugarHelper>? logger = null)
        : base(new SqlServerSugarOptions { ConnectionString = connectionString }, logger)
    {
        _sqlServerOptions = new SqlServerSugarOptions { ConnectionString = connectionString };
    }

    private static SqlSugarOptions CreateOptions(SqlServerSugarOptions options)
    {
        options.AutoSetConnectionString();
        return options;
    }

    #region SQL Server 特有功能

    /// <summary>
    /// 高性能批量插入（使用 BulkCopy）
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
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体集合</param>
    /// <returns>插入行数</returns>
    public async Task<int> BulkInsertAsync<T>(List<T> entities) where T : class, new()
    {
        return await ((SqlSugarClient)Db).Fastest<T>().BulkCopyAsync(entities);
    }

    /// <summary>
    /// 高性能批量更新
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体集合</param>
    /// <returns>更新行数</returns>
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
    /// 批量合并（插入或更新）
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
    /// 异步执行存储过程
    /// </summary>
    public async Task<int> ExecuteStoredProcedureAsync(string procedureName, object? parameters = null)
    {
        if (parameters == null)
        {
            return await Db.Ado.UseStoredProcedure().ExecuteCommandAsync(procedureName);
        }
        return await Db.Ado.UseStoredProcedure().ExecuteCommandAsync(procedureName, parameters);
    }

    /// <summary>
    /// 执行存储过程并返回结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="procedureName">存储过程名称</param>
    /// <param name="parameters">参数</param>
    /// <returns>查询结果</returns>
    public List<T> ExecuteStoredProcedure<T>(string procedureName, object? parameters = null) where T : class, new()
    {
        if (parameters == null)
        {
            return Db.Ado.UseStoredProcedure().SqlQuery<T>(procedureName);
        }
        return Db.Ado.UseStoredProcedure().SqlQuery<T>(procedureName, parameters);
    }

    /// <summary>
    /// 异步执行存储过程并返回结果
    /// </summary>
    public async Task<List<T>> ExecuteStoredProcedureAsync<T>(string procedureName, object? parameters = null) where T : class, new()
    {
        if (parameters == null)
        {
            return await Db.Ado.UseStoredProcedure().SqlQueryAsync<T>(procedureName);
        }
        return await Db.Ado.UseStoredProcedure().SqlQueryAsync<T>(procedureName, parameters);
    }

    /// <summary>
    /// 获取数据库信息
    /// </summary>
    /// <returns>数据库信息</returns>
    public SqlServerDatabaseInfo GetDatabaseInfo()
    {
        var sql = @"
            SELECT 
                DB_NAME() AS DatabaseName,
                SUM(CASE WHEN type = 0 THEN size END) * 8 / 1024.0 AS DataSizeMB,
                SUM(CASE WHEN type = 1 THEN size END) * 8 / 1024.0 AS LogSizeMB,
                SUM(size) * 8 / 1024.0 AS TotalSizeMB
            FROM sys.database_files";

        return Db.Ado.SqlQuerySingle<SqlServerDatabaseInfo>(sql);
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
    /// 截断表
    /// </summary>
    /// <param name="tableName">表名</param>
        public void TruncateTable(string tableName)
        {
            Db.DbMaintenance.TruncateTable(tableName);
        }

        /// <summary>
        /// 收缩数据库
        /// </summary>
        public void ShrinkDatabase()
        {
            var dbName = Db.Ado.GetScalar("SELECT DB_NAME()")?.ToString();
            ExecuteSql($"DBCC SHRINKDATABASE(N'{dbName}')");
        }

        #endregion
    }
