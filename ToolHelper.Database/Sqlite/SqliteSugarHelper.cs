using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlSugar;
using ToolHelper.Database.Abstractions;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Core;

namespace ToolHelper.Database.Sqlite;

/// <summary>
/// SQLite 数据库帮助类（基于 SqlSugar ORM）
/// 提供 SQLite 数据库的 ORM 操作
/// </summary>
/// <example>
/// <code>
/// // 创建 SQLite 帮助类
/// var options = Options.Create(new SqliteSugarOptions
/// {
///     DatabasePath = "mydata.db"
/// });
/// 
/// using var db = new SqliteSugarHelper(options);
/// 
/// // 自动创建表（根据实体类）
/// db.CreateTable&lt;User&gt;();
/// 
/// // 插入数据（无需写SQL）
/// var user = new User { Name = "张三", Age = 25 };
/// db.Insert(user);
/// 
/// // 查询数据（Lambda表达式）
/// var users = db.GetList&lt;User&gt;(u => u.Age > 18);
/// 
/// // 更新数据
/// user.Age = 26;
/// db.Update(user);
/// 
/// // 条件更新（只更新指定字段）
/// db.Update&lt;User&gt;(
///     u => new User { Age = 30 },
///     u => u.Name == "张三");
/// 
/// // 删除数据
/// db.Delete&lt;User&gt;(u => u.Age &lt; 18);
/// </code>
/// </example>
public class SqliteSugarHelper : SqlSugarDbHelper
{
    private readonly SqliteSugarOptions _sqliteOptions;

    /// <summary>
    /// 创建 SqliteSugarHelper 实例
    /// </summary>
    /// <param name="options">SQLite 配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
    public SqliteSugarHelper(IOptions<SqliteSugarOptions> options, ILogger<SqliteSugarHelper>? logger = null)
        : base(CreateOptions(options.Value), logger)
    {
        _sqliteOptions = options.Value;
    }

    /// <summary>
    /// 使用配置创建实例
    /// </summary>
    /// <param name="options">配置选项</param>
    /// <param name="logger">日志记录器</param>
    public SqliteSugarHelper(SqliteSugarOptions options, ILogger<SqliteSugarHelper>? logger = null)
        : base(CreateOptions(options), logger)
    {
        _sqliteOptions = options;
    }

    /// <summary>
    /// 使用数据库路径创建实例
    /// </summary>
    /// <param name="databasePath">数据库文件路径</param>
    /// <param name="logger">日志记录器</param>
    public SqliteSugarHelper(string databasePath, ILogger<SqliteSugarHelper>? logger = null)
        : base(CreateOptions(new SqliteSugarOptions { DatabasePath = databasePath }), logger)
    {
        _sqliteOptions = new SqliteSugarOptions { DatabasePath = databasePath };
    }

    private static SqlSugarOptions CreateOptions(SqliteSugarOptions options)
    {
        options.AutoSetConnectionString();
        return options;
    }

    #region SQLite 特有功能

    /// <summary>
    /// 执行 VACUUM 操作（整理数据库）
    /// </summary>
    public void Vacuum()
    {
        ExecuteSql("VACUUM");
    }

    /// <summary>
    /// 异步执行 VACUUM 操作
    /// </summary>
    public async Task VacuumAsync()
    {
        await ExecuteSqlAsync("VACUUM");
    }

    /// <summary>
    /// 执行完整性检查
    /// </summary>
    /// <returns>检查结果列表</returns>
    public List<string> IntegrityCheck()
    {
        var result = Db.Ado.GetDataTable("PRAGMA integrity_check");
        var list = new List<string>();
        foreach (System.Data.DataRow row in result.Rows)
        {
            list.Add(row[0]?.ToString() ?? "ok");
        }
        return list;
    }

    /// <summary>
    /// 获取数据库大小（字节）
    /// </summary>
    /// <returns>数据库大小</returns>
    public long GetDatabaseSize()
    {
        var pageCount = Convert.ToInt64(Db.Ado.GetScalar("PRAGMA page_count"));
        var pageSize = Convert.ToInt64(Db.Ado.GetScalar("PRAGMA page_size"));
        return pageCount * pageSize;
    }

    /// <summary>
    /// 设置 WAL 模式
    /// </summary>
    public void EnableWalMode()
    {
        ExecuteSql("PRAGMA journal_mode = WAL");
    }

    /// <summary>
    /// 获取当前日志模式
    /// </summary>
    /// <returns>日志模式</returns>
    public string GetJournalMode()
    {
        return Db.Ado.GetScalar("PRAGMA journal_mode")?.ToString() ?? "delete";
    }

    /// <summary>
    /// 备份数据库到文件
    /// </summary>
    /// <param name="backupPath">备份文件路径</param>
    public void BackupTo(string backupPath)
    {
        ExecuteSql($"VACUUM INTO '{backupPath}'");
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
            /// 获取表的列信息
            /// </summary>
            /// <param name="tableName">表名</param>
            /// <returns>列信息列表</returns>
            public List<DbColumnInfo> GetTableColumns(string tableName)
            {
                return Db.DbMaintenance.GetColumnInfosByTableName(tableName);
            }

            /// <summary>
            /// 关闭所有连接（用于确保 SQLite 文件可以被删除）
            /// </summary>
            public void CloseAllConnections()
            {
                try
                {
                    Db.Close();
                    // SQLite 特定：清除连接池
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"关闭连接时发生错误: {ex.Message}");
                }
            }

            /// <summary>
            /// 释放资源（重写以确保 SQLite 文件被正确释放）
            /// </summary>
            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    CloseAllConnections();
                }
                base.Dispose(disposing);
            }

            #endregion
        }
