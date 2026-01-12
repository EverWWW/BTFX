using System.IO;
using BTFX.Common;
using Microsoft.Extensions.Options;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Sqlite;

namespace BTFX.Data;

/// <summary>
/// 数据库工厂类
/// 提供创建 SqliteHelper 实例的工厂方法
/// </summary>
public static class DatabaseFactory
{
    private static string? _databasePath;
    private static readonly object _lock = new();

    /// <summary>
    /// 获取数据库路径
    /// </summary>
    public static string DatabasePath
    {
        get
        {
            if (_databasePath == null)
            {
                lock (_lock)
                {
                    _databasePath ??= GetDefaultDatabasePath();
                }
            }
            return _databasePath;
        }
    }

    /// <summary>
    /// 获取默认数据库路径
    /// </summary>
    private static string GetDefaultDatabasePath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dbDir = Path.Combine(baseDir, Constants.DATABASE_DIRECTORY);

        // 确保目录存在
        if (!Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        return Path.Combine(dbDir, Constants.DATABASE_FILENAME);
    }

    /// <summary>
    /// 创建 SqliteHelper 实例
    /// 调用者负责 Dispose
    /// </summary>
    /// <returns>SqliteHelper 实例</returns>
    public static SqliteHelper CreateSqliteHelper()
    {
        var options = Options.Create(new SqliteOptions
        {
            DatabasePath = DatabasePath,
            JournalMode = SqliteJournalMode.Wal,
            SynchronousMode = SqliteSynchronousMode.Normal,
            ForeignKeys = true,
            EnableLogging = false
        });

        return new SqliteHelper(options);
    }

    /// <summary>
    /// 创建已初始化的 SqliteHelper 实例
    /// 调用者负责 Dispose
    /// </summary>
    /// <returns>已初始化的 SqliteHelper 实例</returns>
    public static async Task<SqliteHelper> CreateAndInitializeSqliteHelperAsync()
    {
        var helper = CreateSqliteHelper();
        await helper.InitializeAsync();
        return helper;
    }
}
