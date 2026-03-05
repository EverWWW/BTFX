using System.IO;
using BTFX.Common;
using BTFX.Helpers;
using BTFX.Models;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Sqlite;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Data;

/// <summary>
/// 数据库初始化器
/// 负责创建数据库、表结构和初始数据
/// 使用 SqlSugar ORM 进行数据库操作
/// </summary>
public class DatabaseInitializer
{
    /// <summary>
    /// 当前数据库版本
    /// 版本历史:
    /// - 1: 初始版本
    /// - 2: 测量评估模块扩展（新增 MeasurementRecord 字段：MeasurementName, MeasurementType, FrontVideoPath, SideVideoPath 等）
    /// </summary>
    public const int CurrentDatabaseVersion = 2;

    private readonly string _databasePath;
    private readonly ILogHelper? _logHelper;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="logHelper">日志助手（可选）</param>
    public DatabaseInitializer(ILogHelper? logHelper = null)
    {
        _logHelper = logHelper;

        // 数据库路径
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dbDir = Path.Combine(baseDir, Constants.DATABASE_DIRECTORY);

        // 确保目录存在
        if (!Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        _databasePath = Path.Combine(dbDir, Constants.DATABASE_FILENAME);
    }

    /// <summary>
    /// 获取数据库路径
    /// </summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// 创建 SqliteSugarHelper 实例
    /// </summary>
    public SqliteSugarHelper CreateSqliteSugarHelper()
    {
        var options = new SqliteSugarOptions
        {
            DatabasePath = _databasePath,
            EnableSqlLog = false
        };

        return new SqliteSugarHelper(options);
    }

    /// <summary>
    /// 初始化数据库
    /// </summary>
    public async Task InitializeAsync()
    {
        _logHelper?.Information("开始初始化数据库...");

        Exception? firstException = null;

        try
        {
            await InitializeDatabaseCoreAsync();
            return; // 成功，直接返回
        }
        catch (Exception ex)
        {
            firstException = ex;
            _logHelper?.Warning($"数据库初始化失败，准备重建数据库: {ex.Message}");
        }

        // 强制进行垃圾回收，释放所有数据库连接
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // 等待一小段时间让文件句柄完全释放
        await Task.Delay(500);

        // 尝试删除数据库并重新初始化
        try
        {
            DeleteDatabaseSafe();
            _logHelper?.Information("已删除旧数据库，重新初始化...");

            await InitializeDatabaseCoreAsync();
            _logHelper?.Information("数据库重建成功");
        }
        catch (Exception retryEx)
        {
            // 构建详细的错误信息
            var fullMessage = retryEx.Message;
            var inner = retryEx.InnerException;
            while (inner != null)
            {
                fullMessage += $"\n内部异常: {inner.Message}";
                inner = inner.InnerException;
            }

            _logHelper?.Error($"数据库重建失败: {fullMessage}\n堆栈: {retryEx.StackTrace}", retryEx);

            // 如果重建也失败，抛出原始异常（更有价值）
            var originalMessage = firstException?.Message ?? retryEx.Message;
            throw new Exception($"数据库初始化失败: {originalMessage}", firstException ?? retryEx);
        }
    }

    /// <summary>
    /// 安全删除数据库（处理文件被占用的情况）
    /// </summary>
    private void DeleteDatabaseSafe()
    {
        // 清理 SQLite 连接池
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        var files = new[]
        {
            _databasePath,
            _databasePath + "-wal",
            _databasePath + "-shm",
            _databasePath + "-journal"
        };

        foreach (var file in files)
        {
            if (!File.Exists(file)) continue;

            for (int retry = 0; retry < 5; retry++)
            {
                try
                {
                    File.Delete(file);
                    break;
                }
                catch (IOException) when (retry < 4)
                {
                    // 文件被占用，等待后重试
                    Thread.Sleep(200 * (retry + 1));
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }
    }

    /// <summary>
    /// 数据库初始化核心逻辑
    /// </summary>
    private async Task InitializeDatabaseCoreAsync()
    {
        using var db = CreateSqliteSugarHelper();

        // 检查数据库版本
        var version = GetDatabaseVersion(db);
        _logHelper?.Information($"当前数据库版本: {version}");

        if (version == 0)
        {
            // 新数据库，创建所有表和初始数据
            _logHelper?.Information("创建数据库表结构...");
            await CreateTablesAsync(db);

            _logHelper?.Information("初始化内置数据...");
            await SeedDataAsync(db);
            
            SetDatabaseVersion(db, CurrentDatabaseVersion);
            _logHelper?.Information($"数据库初始化完成，版本: {CurrentDatabaseVersion}");
        }
        else if (version < CurrentDatabaseVersion)
        {
            // 需要升级
            _logHelper?.Information($"升级数据库: {version} -> {CurrentDatabaseVersion}");
            await UpgradeDatabaseAsync(db, version);
            SetDatabaseVersion(db, CurrentDatabaseVersion);
            _logHelper?.Information("数据库升级完成");
        }
        else
        {
            _logHelper?.Information("数据库已是最新版本");
        }
    }

    /// <summary>
    /// 获取数据库版本
    /// </summary>
    private int GetDatabaseVersion(SqliteSugarHelper db)
    {
        try
        {
            var result = db.SqlQueryScalar<int>("PRAGMA user_version;");
            return result;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 设置数据库版本
    /// </summary>
    private void SetDatabaseVersion(SqliteSugarHelper db, int version)
    {
        db.ExecuteSql($"PRAGMA user_version = {version};");
    }

    /// <summary>
    /// 创建所有表（使用 CodeFirst）
    /// </summary>
    private Task CreateTablesAsync(SqliteSugarHelper db)
    {
        _logHelper?.Information("使用 CodeFirst 创建表结构...");

        // 按依赖顺序创建表
        db.CreateTables(
            typeof(Department),
            typeof(User),
            typeof(Patient),
            typeof(MeasurementRecord),
            typeof(GaitParameters),
            typeof(Report),
            typeof(SystemSetting)
        );

        // 创建索引（SqlSugar CodeFirst 不自动创建索引，需要手动执行）
        CreateIndexes(db);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 创建索引
    /// </summary>
    private void CreateIndexes(SqliteSugarHelper db)
    {
        _logHelper?.Information("创建数据库索引...");

        // Users 表索引
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Users_Username ON Users(Username);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Users_Phone ON Users(Phone);");

        // Patients 表索引
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_Name ON Patients(Name);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_Phone ON Patients(Phone);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_IdNumber ON Patients(IdNumber);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_Status ON Patients(Status);");

        // MeasurementRecords 表索引
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_PatientId ON MeasurementRecords(PatientId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_MeasurementDate ON MeasurementRecords(MeasurementDate);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_Status ON MeasurementRecords(Status);");

        // Reports 表索引
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Reports_ReportNumber ON Reports(ReportNumber);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Reports_PatientId ON Reports(PatientId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Reports_ReportDate ON Reports(ReportDate);");
    }

    /// <summary>
    /// 初始化内置数据
    /// </summary>
    private async Task SeedDataAsync(SqliteSugarHelper db)
    {
        var now = DateTime.Now;

        // 创建默认科室
        try
        {
            _logHelper?.Information("Step 1: 创建默认科室...");
            
            // 检查是否已存在
            if (!db.Any<Department>(d => d.Name == "默认科室"))
            {
                await db.InsertAsync(new Department
                {
                    Name = "默认科室",
                    Phone = "",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            _logHelper?.Information("Step 1: 完成");
        }
        catch (Exception ex)
        {
            throw new Exception($"Step 1 失败 (Departments): {ex.Message}", ex);
        }

        // 创建内置用户
        var adminSalt = PasswordHelper.GenerateSalt();
        var adminHash = PasswordHelper.HashPassword(Constants.DEFAULT_PASSWORD, adminSalt);
        var userSalt = PasswordHelper.GenerateSalt();
        var userHash = PasswordHelper.HashPassword(Constants.DEFAULT_PASSWORD, userSalt);

        try
        {
            _logHelper?.Information("Step 2: 创建 admin 用户...");
            
            if (!await db.AnyAsync<User>(u => u.Username == Constants.ADMIN_USERNAME))
            {
                await db.InsertAsync(new User
                {
                    Username = Constants.ADMIN_USERNAME,
                    PasswordHash = adminHash,
                    PasswordSalt = adminSalt,
                    Name = "管理员",
                    Phone = "",
                    Role = UserRole.Administrator,
                    IsEnabled = true,
                    IsBuiltIn = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            _logHelper?.Information("Step 2: 完成");
        }
        catch (Exception ex)
        {
            throw new Exception($"Step 2 失败 (admin): {ex.Message}", ex);
        }

        try
        {
            _logHelper?.Information("Step 3: 创建 user 用户...");
            
            if (!await db.AnyAsync<User>(u => u.Username == Constants.USER_USERNAME))
            {
                await db.InsertAsync(new User
                {
                    Username = Constants.USER_USERNAME,
                    PasswordHash = userHash,
                    PasswordSalt = userSalt,
                    Name = "操作员",
                    Phone = "",
                    Role = UserRole.Operator,
                    IsEnabled = true,
                    IsBuiltIn = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            _logHelper?.Information("Step 3: 完成");
        }
        catch (Exception ex)
        {
            throw new Exception($"Step 3 失败 (user): {ex.Message}", ex);
        }

        // 初始化系统设置
        var settings = new (string Key, string Value, string Type)[]
        {
            ("UnitName", "", "string"),
            ("LogoPath", "", "string"),
            ("AutoBackupEnabled", "false", "bool"),
            ("AutoBackupFrequency", "0", "int"),
            ("AutoBackupTime", "02:00", "string"),
            ("AutoBackupRetainCount", "7", "int")
        };

        for (int i = 0; i < settings.Length; i++)
        {
            var (key, value, type) = settings[i];
            try
            {
                _logHelper?.Information($"Step 4.{i + 1}: 创建设置 {key}...");
                
                if (!await db.AnyAsync<SystemSetting>(s => s.SettingKey == key))
                {
                    await db.InsertAsync(new SystemSetting
                    {
                        SettingKey = key,
                        SettingValue = value,
                        ValueType = type,
                        UpdatedAt = now
                    });
                }
                _logHelper?.Information($"Step 4.{i + 1}: 完成");
            }
            catch (Exception ex)
            {
                throw new Exception($"Step 4.{i + 1} 失败 (设置 {key}): {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// 升级数据库
    /// </summary>
    private Task UpgradeDatabaseAsync(SqliteSugarHelper db, int fromVersion)
    {
        // 按版本号执行升级脚本
        for (int version = fromVersion + 1; version <= CurrentDatabaseVersion; version++)
        {
            _logHelper?.Information($"执行升级脚本 v{version}...");

            switch (version)
            {
                case 1:
                    // v0 -> v1: 初始版本，无升级脚本
                    break;

                case 2:
                    // v1 -> v2: 测量评估模块扩展
                    UpgradeToV2(db);
                    break;

                // 未来版本升级在此添加
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 升级到 v2: 测量评估模块扩展字段
    /// </summary>
    private void UpgradeToV2(SqliteSugarHelper db)
    {
        _logHelper?.Information("升级到 v2: 添加测量评估模块字段...");

        // 使用 CodeFirst 自动同步表结构（SqlSugar 会自动添加缺失的列）
        db.CreateTables(typeof(MeasurementRecord));

        // 也可以手动添加列（如果 CodeFirst 不生效）
        var alterStatements = new[]
        {
            "ALTER TABLE MeasurementRecords ADD COLUMN MeasurementName TEXT;",
            "ALTER TABLE MeasurementRecords ADD COLUMN MeasurementType INTEGER DEFAULT 0 NOT NULL;",
            "ALTER TABLE MeasurementRecords ADD COLUMN FrontVideoPath TEXT;",
            "ALTER TABLE MeasurementRecords ADD COLUMN SideVideoPath TEXT;",
            "ALTER TABLE MeasurementRecords ADD COLUMN VideoSpec INTEGER DEFAULT 0 NOT NULL;",
            "ALTER TABLE MeasurementRecords ADD COLUMN WalkwayLength REAL DEFAULT 6.0 NOT NULL;",
            "ALTER TABLE MeasurementRecords ADD COLUMN ImportStrategy INTEGER DEFAULT 0 NOT NULL;",
            "ALTER TABLE MeasurementRecords ADD COLUMN VideoImportMode INTEGER DEFAULT 0 NOT NULL;",
            "ALTER TABLE MeasurementRecords ADD COLUMN CurrentAnalysisStage INTEGER DEFAULT 0 NOT NULL;",
            "ALTER TABLE MeasurementRecords ADD COLUMN KeypointsCompleted INTEGER DEFAULT 0 NOT NULL;",
            "ALTER TABLE MeasurementRecords ADD COLUMN EventsCompleted INTEGER DEFAULT 0 NOT NULL;",
            "ALTER TABLE MeasurementRecords ADD COLUMN KinematicsCompleted INTEGER DEFAULT 0 NOT NULL;",
            "ALTER TABLE MeasurementRecords ADD COLUMN MeasurementFolderPath TEXT;"
        };

        foreach (var sql in alterStatements)
        {
            try
            {
                db.ExecuteSql(sql);
            }
            catch (Exception ex)
            {
                // 列可能已存在，忽略错误
                _logHelper?.Debug($"列可能已存在: {ex.Message}");
            }
        }

        _logHelper?.Information("v2 升级完成");
    }

    /// <summary>
    /// 检查数据库是否存在
    /// </summary>
    public bool DatabaseExists()
    {
        return File.Exists(_databasePath);
    }

    /// <summary>
    /// 删除数据库（仅用于测试）
    /// </summary>
    public void DeleteDatabase()
    {
        // 清理连接池
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }

        // 同时删除 WAL 和 SHM 文件
        var walPath = _databasePath + "-wal";
        var shmPath = _databasePath + "-shm";

        if (File.Exists(walPath))
            File.Delete(walPath);
        if (File.Exists(shmPath))
            File.Delete(shmPath);
    }
}
