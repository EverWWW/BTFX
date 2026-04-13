using System.IO;
using BTFX.Common;
using BTFX.Helpers;
using BTFX.Models;
using BTFX.Models.Analysis;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Sqlite;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Data;

/// <summary>
/// 数据库初始化器。
/// 负责创建数据库文件、建表、建索引、写入种子数据，以及执行版本迁移。
/// 通过 SQLite PRAGMA user_version 管理数据库版本号。
/// </summary>
public class DatabaseInitializer
{
    /// <summary>
    /// 当前数据库目标版本号。每次结构变更时递增，并在 <see cref="UpgradeDatabaseAsync"/> 中添加对应迁移逻辑。
    /// </summary>
    public const int CurrentDatabaseVersion = 4;

    private readonly string _databasePath;
    private readonly ILogHelper? _logHelper;

    /// <summary>
    /// 初始化 <see cref="DatabaseInitializer"/>，自动确定数据库文件路径并创建所需目录。
    /// </summary>
    /// <param name="logHelper">可选的日志记录器。</param>
    public DatabaseInitializer(ILogHelper? logHelper = null)
    {
        _logHelper = logHelper;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dbDir = Path.Combine(baseDir, Constants.DATABASE_DIRECTORY);

        if (!Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        _databasePath = Path.Combine(dbDir, Constants.DATABASE_FILENAME);
    }

    /// <summary>
    /// 获取数据库文件的完整路径。
    /// </summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// 创建并返回一个新的 <see cref="SqliteSugarHelper"/> 实例。
    /// 每次调用返回独立实例，调用方负责释放。
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
    /// 异步初始化数据库。
    /// 首次尝试正常初始化；若失败，释放连接、删除损坏文件后重建。
    /// 两次均失败时抛出异常，并保留原始异常信息。
    /// </summary>
    public async Task InitializeAsync()
    {
        _logHelper?.Information("开始初始化数据库...");

        Exception? firstException = null;

        try
        {
            await InitializeDatabaseCoreAsync();
            return;
        }
        catch (Exception ex)
        {
            firstException = ex;
            _logHelper?.Warning($"数据库初始化失败，准备删除重建：{ex.Message}");
        }

        // 释放所有 SQLite 连接，等待文件句柄释放
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        await Task.Delay(500);

        try
        {
            DeleteDatabaseSafe();
            _logHelper?.Information("已删除旧数据库文件，重新初始化...");

            await InitializeDatabaseCoreAsync();
            _logHelper?.Information("数据库重建成功。");
        }
        catch (Exception retryEx)
        {
            var fullMessage = retryEx.Message;
            var inner = retryEx.InnerException;
            while (inner != null)
            {
                fullMessage += $"\n内部异常：{inner.Message}";
                inner = inner.InnerException;
            }

            _logHelper?.Error($"数据库重建失败：{fullMessage}\n堆栈：{retryEx.StackTrace}", retryEx);

            var originalMessage = firstException?.Message ?? retryEx.Message;
            throw new Exception($"数据库初始化失败：{originalMessage}", firstException ?? retryEx);
        }
    }

    /// <summary>
    /// 安全删除数据库文件及其 WAL/SHM/Journal 附属文件。
    /// 先清空连接池，再对每个文件最多重试 5 次（间隔递增）。
    /// </summary>
    private void DeleteDatabaseSafe()
    {
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
                    Thread.Sleep(200 * (retry + 1));
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }
    }

    /// <summary>
    /// 数据库初始化核心逻辑。
    /// 读取当前版本号：0 表示全新数据库（建表+种子数据）；
    /// 低于目标版本则执行迁移；等于目标版本则跳过。
    /// </summary>
    private async Task InitializeDatabaseCoreAsync()
    {
        using var db = CreateSqliteSugarHelper();
        var version = GetDatabaseVersion(db);
        _logHelper?.Information($"当前数据库版本：{version}");

        if (version == 0)
        {
            _logHelper?.Information("检测到新数据库，开始创建表结构...");
            await CreateTablesAsync(db);

            _logHelper?.Information("表结构创建完成，开始写入种子数据...");
            await SeedDataAsync(db);

            SetDatabaseVersion(db, CurrentDatabaseVersion);
            _logHelper?.Information($"数据库初始化完成，版本设置为 {CurrentDatabaseVersion}。");
        }
        else if (version < CurrentDatabaseVersion)
        {
            _logHelper?.Information($"数据库需要升级：{version} → {CurrentDatabaseVersion}");
            await UpgradeDatabaseAsync(db, version);
            SetDatabaseVersion(db, CurrentDatabaseVersion);
            _logHelper?.Information("数据库升级完成。");
        }
        else
        {
            _logHelper?.Information("数据库版本已是最新，无需迁移。");
        }
    }

    /// <summary>
    /// 读取 SQLite PRAGMA user_version 作为数据库版本号。
    /// 读取失败时返回 0（视为全新数据库）。
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
    /// 将指定版本号写入 SQLite PRAGMA user_version。
    /// </summary>
    private void SetDatabaseVersion(SqliteSugarHelper db, int version)
    {
        db.ExecuteSql($"PRAGMA user_version = {version};");
    }

    /// <summary>
    /// 创建所有业务表并建立索引。
    /// </summary>
    private Task CreateTablesAsync(SqliteSugarHelper db)
    {
        _logHelper?.Information("创建数据表...");
        db.CreateTables(
            typeof(Department),
            typeof(User),
            typeof(Patient),
            typeof(MeasurementRecord),
            typeof(GaitParameters),
            typeof(Report),
            typeof(SystemSetting),
            typeof(AnalysisResult),
            typeof(KinematicSummary),
            typeof(AnalysisCsvFile),
            typeof(QualityControlInfo)
        );

        CreateIndexes(db);

        return Task.CompletedTask;
    }

    /// <summary>
    /// 为高频查询字段创建索引，使用 IF NOT EXISTS 保证幂等性。
    /// </summary>
    private void CreateIndexes(SqliteSugarHelper db)
    {
        _logHelper?.Information("创建数据库索引...");

        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Users_Username ON Users(Username);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Users_Phone ON Users(Phone);");

        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_Name ON Patients(Name);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_Phone ON Patients(Phone);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_IdNumber ON Patients(IdNumber);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_Status ON Patients(Status);");

        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_PatientId ON MeasurementRecords(PatientId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_MeasurementDate ON MeasurementRecords(MeasurementDate);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_Status ON MeasurementRecords(Status);");

        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Reports_ReportNumber ON Reports(ReportNumber);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Reports_PatientId ON Reports(PatientId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Reports_ReportDate ON Reports(ReportDate);");

        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_MeasurementId ON AnalysisResults(MeasurementId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_RequestId ON AnalysisResults(RequestId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_Success ON AnalysisResults(Success);");

        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_KinematicSummaries_AnalysisResultId ON KinematicSummaries(AnalysisResultId);");

        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisCsvFiles_AnalysisResultId ON AnalysisCsvFiles(AnalysisResultId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisCsvFiles_FileType ON AnalysisCsvFiles(FileType);");

        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_QualityControls_AnalysisResultId ON QualityControls(AnalysisResultId);");
    }

    /// <summary>
    /// 写入初始种子数据，包括默认科室、内置管理员账户、内置普通账户及系统配置项。
    /// 所有插入均先检查是否已存在，保证幂等性。
    /// </summary>
    private async Task SeedDataAsync(SqliteSugarHelper db)
    {
        var now = DateTime.Now;

        // Step 1：创建默认科室
        try
        {
            _logHelper?.Information("Step 1：创建默认科室...");

            if (!db.Any<Department>(d => d.Name == ""))
            {
                await db.InsertAsync(new Department
                {
                    Name = "",
                    Phone = "",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            _logHelper?.Information("Step 1：默认科室创建完成。");
        }
        catch (Exception ex)
        {
            throw new Exception($"创建默认科室失败：{ex.Message}", ex);
        }

        var adminSalt = PasswordHelper.GenerateSalt();
        var adminHash = PasswordHelper.HashPassword(Constants.DEFAULT_PASSWORD, adminSalt);
        var userSalt = PasswordHelper.GenerateSalt();
        var userHash = PasswordHelper.HashPassword(Constants.DEFAULT_PASSWORD, userSalt);

        // Step 2：创建内置管理员账户
        try
        {
            _logHelper?.Information("Step 2：创建内置管理员账户...");

            if (!await db.AnyAsync<User>(u => u.Username == Constants.ADMIN_USERNAME))
            {
                await db.InsertAsync(new User
                {
                    Username = Constants.ADMIN_USERNAME,
                    PasswordHash = adminHash,
                    PasswordSalt = adminSalt,
                    Name = "",
                    Phone = "",
                    Role = UserRole.Administrator,
                    IsEnabled = true,
                    IsBuiltIn = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            _logHelper?.Information("Step 2：内置管理员账户创建完成。");
        }
        catch (Exception ex)
        {
            throw new Exception($"Step 2 创建管理员失败：{ex.Message}", ex);
        }

        // Step 3：创建内置普通账户
        try
        {
            _logHelper?.Information("Step 3：创建内置普通账户...");

            if (!await db.AnyAsync<User>(u => u.Username == Constants.USER_USERNAME))
            {
                await db.InsertAsync(new User
                {
                    Username = Constants.USER_USERNAME,
                    PasswordHash = userHash,
                    PasswordSalt = userSalt,
                    Name = "普通账户",
                    Phone = "",
                    Role = UserRole.Operator,
                    IsEnabled = true,
                    IsBuiltIn = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            _logHelper?.Information("Step 3：内置普通账户创建完成。");
        }
        catch (Exception ex)
        {
            throw new Exception($"Step 3 创建普通账户失败：{ex.Message}", ex);
        }

        // Step 4：写入系统配置默认值
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
                _logHelper?.Information($"Step 4.{i + 1}：写入系统配置 {key}...");

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
                _logHelper?.Information($"Step 4.{i + 1}：系统配置 {key} 写入完成。");
            }
            catch (Exception ex)
            {
                throw new Exception($"Step 4.{i + 1} 写入系统配置（{key}）失败：{ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// 按版本号顺序依次执行数据库结构迁移，从 <paramref name="fromVersion"/> 升级到 <see cref="CurrentDatabaseVersion"/>。
    /// </summary>
    /// <param name="db">数据库连接。</param>
    /// <param name="fromVersion">当前数据库版本号。</param>
    private Task UpgradeDatabaseAsync(SqliteSugarHelper db, int fromVersion)
    {
        for (int version = fromVersion + 1; version <= CurrentDatabaseVersion; version++)
        {
            _logHelper?.Information($"执行 v{version} 升级...");

            switch (version)
            {
                case 1:
                    // v1 为初始版本，无需迁移操作
                    break;

                case 2:
                    UpgradeToV2(db);
                    break;

                case 3:
                    UpgradeToV3(db);
                    break;

                case 4:
                    UpgradeToV4(db);
                    break;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 升级到 v2：新增 MeasurementRecords 表并添加测量相关扩展字段。
    /// 使用 ALTER TABLE ADD COLUMN，对已存在的列忽略错误（容错处理）。
    /// </summary>
    private void UpgradeToV2(SqliteSugarHelper db)
    {
        _logHelper?.Information("升级到 v2：扩展 MeasurementRecords 表字段...");

        db.CreateTables(typeof(MeasurementRecord));

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
                // 列已存在时 SQLite 会报错，属于正常情况，记录调试日志后继续
                _logHelper?.Debug($"v2 迁移跳过（列可能已存在）：{ex.Message}");
            }
        }

        _logHelper?.Information("v2 升级完成。");
    }

    /// <summary>
    /// 升级到 v3：新增分析结果相关表（AnalysisResult、KinematicSummary、AnalysisCsvFile、QualityControlInfo），
    /// 并为 GaitParameters 添加详细步态参数字段，同时创建对应索引。
    /// </summary>
    private void UpgradeToV3(SqliteSugarHelper db)
    {
        _logHelper?.Information("升级到 v3：新增分析结果相关表及字段...");

        db.CreateTables(
            typeof(AnalysisResult),
            typeof(KinematicSummary),
            typeof(AnalysisCsvFile),
            typeof(QualityControlInfo)
        );

        var alterStatements = new[]
        {
            "ALTER TABLE GaitParameters ADD COLUMN AnalysisResultId INTEGER;",
            "ALTER TABLE GaitParameters ADD COLUMN GaitCycleDurationS REAL;",
            "ALTER TABLE GaitParameters ADD COLUMN StanceTimeS REAL;",
            "ALTER TABLE GaitParameters ADD COLUMN SwingTimeS REAL;",
            "ALTER TABLE GaitParameters ADD COLUMN DoubleSupportTimeS REAL;",
            "ALTER TABLE GaitParameters ADD COLUMN SingleSupportTimeS REAL;",
            "ALTER TABLE GaitParameters ADD COLUMN StepLengthM REAL;",
            "ALTER TABLE GaitParameters ADD COLUMN StrideLengthM REAL;",
            "ALTER TABLE GaitParameters ADD COLUMN GaitSpeedMPerS REAL;"
        };

        foreach (var sql in alterStatements)
        {
            try
            {
                db.ExecuteSql(sql);
            }
            catch (Exception ex)
            {
                // 列已存在时忽略，记录调试日志
                _logHelper?.Debug($"v3 迁移跳过（列可能已存在）：{ex.Message}");
            }
        }

        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_MeasurementId ON AnalysisResults(MeasurementId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_RequestId ON AnalysisResults(RequestId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_Success ON AnalysisResults(Success);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_KinematicSummaries_AnalysisResultId ON KinematicSummaries(AnalysisResultId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisCsvFiles_AnalysisResultId ON AnalysisCsvFiles(AnalysisResultId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisCsvFiles_FileType ON AnalysisCsvFiles(FileType);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_QualityControls_AnalysisResultId ON QualityControls(AnalysisResultId);");

        _logHelper?.Information("v3 升级完成。");
    }

    /// <summary>
    /// 升级到 v4：为 Patients 表添加 BirthDate 字段（TEXT 类型，存储 ISO 8601 日期字符串）。
    /// </summary>
    private void UpgradeToV4(SqliteSugarHelper db)
    {
        _logHelper?.Information("升级到 v4：为 Patients 表添加 BirthDate 字段...");

        var alterStatements = new[]
        {
            "ALTER TABLE Patients ADD COLUMN BirthDate TEXT;"
        };

        foreach (var sql in alterStatements)
        {
            try
            {
                db.ExecuteSql(sql);
            }
            catch (Exception ex)
            {
                // 列已存在时忽略，记录调试日志
                _logHelper?.Debug($"v4 迁移跳过（列可能已存在）：{ex.Message}");
            }
        }

        _logHelper?.Information("v4 升级完成。");
    }

    /// <summary>
    /// 检查数据库文件是否存在。
    /// </summary>
    /// <returns>文件存在返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public bool DatabaseExists()
    {
        return File.Exists(_databasePath);
    }

    /// <summary>
    /// 删除数据库文件及其 WAL/SHM 附属文件。
    /// 先清空连接池以释放文件锁，再执行删除。
    /// </summary>
    public void DeleteDatabase()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }

        var walPath = _databasePath + "-wal";
        var shmPath = _databasePath + "-shm";

        if (File.Exists(walPath))
            File.Delete(walPath);
        if (File.Exists(shmPath))
            File.Delete(shmPath);
    }
}
