using System.IO;
using BTFX.Common;
using BTFX.Helpers;
using Microsoft.Extensions.Options;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Sqlite;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Data;

/// <summary>
/// 数据库初始化器
/// 负责创建数据库、表结构和初始数据
/// </summary>
public class DatabaseInitializer
{
    /// <summary>
    /// 当前数据库版本
    /// </summary>
    public const int CurrentDatabaseVersion = 1;

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
    /// 创建 SqliteHelper 实例
    /// </summary>
    public SqliteHelper CreateSqliteHelper()
    {
        var options = Options.Create(new SqliteOptions
        {
            DatabasePath = _databasePath,
            JournalMode = SqliteJournalMode.Wal,
            SynchronousMode = SqliteSynchronousMode.Normal,
            ForeignKeys = true,
            EnableLogging = false
        });

        return new SqliteHelper(options);
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
        SqliteHelper? db = null;
        try
        {
            db = CreateSqliteHelper();
            await db.InitializeAsync();

            // 检查数据库版本
            var version = await GetDatabaseVersionAsync(db);
            _logHelper?.Information($"当前数据库版本: {version}");

            if (version == 0)
            {
                // 新数据库，创建所有表和初始数据
                _logHelper?.Information("创建数据库表结构...");
                await CreateTablesAsync(db);

                _logHelper?.Information("初始化内置数据...");
                await SeedDataAsync(db);
                await SetDatabaseVersionAsync(db, CurrentDatabaseVersion);
                _logHelper?.Information($"数据库初始化完成，版本: {CurrentDatabaseVersion}");
            }
            else if (version < CurrentDatabaseVersion)
            {
                // 需要升级
                _logHelper?.Information($"升级数据库: {version} -> {CurrentDatabaseVersion}");
                await UpgradeDatabaseAsync(db, version);
                await SetDatabaseVersionAsync(db, CurrentDatabaseVersion);
                _logHelper?.Information("数据库升级完成");
            }
            else
            {
                _logHelper?.Information("数据库已是最新版本");
            }
        }
        finally
        {
            // 确保数据库连接被关闭和释放
            if (db != null)
            {
                try
                {
                    await db.CloseAsync();
                }
                catch { }

                try
                {
                    db.Dispose();
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// 获取数据库版本
    /// </summary>
    private async Task<int> GetDatabaseVersionAsync(SqliteHelper db)
    {
        try
        {
            var result = await db.ExecuteScalarAsync<int>("PRAGMA user_version;");
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
    private async Task SetDatabaseVersionAsync(SqliteHelper db, int version)
    {
        await db.ExecuteNonQueryAsync($"PRAGMA user_version = {version};");
    }

    /// <summary>
    /// 创建所有表
    /// </summary>
    private async Task CreateTablesAsync(SqliteHelper db)
    {
        // 按依赖顺序创建表

        // 1. 科室表（无依赖）
        await db.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS Departments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                Phone TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
        ");

        // 2. 用户表（依赖科室）
        await db.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                PasswordSalt TEXT NOT NULL,
                Name TEXT NOT NULL,
                Phone TEXT NOT NULL DEFAULT '',
                Role INTEGER NOT NULL DEFAULT 1,
                DepartmentId INTEGER,
                IsEnabled INTEGER NOT NULL DEFAULT 1,
                IsBuiltIn INTEGER NOT NULL DEFAULT 0,
                LastLoginAt TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (DepartmentId) REFERENCES Departments(Id)
            );
            CREATE INDEX IF NOT EXISTS IX_Users_Username ON Users(Username);
            CREATE INDEX IF NOT EXISTS IX_Users_Phone ON Users(Phone);
        ");

        // 3. 患者表（依赖用户）
        await db.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS Patients (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Gender INTEGER NOT NULL DEFAULT 0,
                BirthDate TEXT,
                Phone TEXT NOT NULL,
                IdNumber TEXT,
                Height REAL,
                Weight REAL,
                Address TEXT,
                MedicalHistory TEXT,
                Remark TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                CreatedBy INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (CreatedBy) REFERENCES Users(Id)
            );
            CREATE INDEX IF NOT EXISTS IX_Patients_Name ON Patients(Name);
            CREATE INDEX IF NOT EXISTS IX_Patients_Phone ON Patients(Phone);
            CREATE INDEX IF NOT EXISTS IX_Patients_IdNumber ON Patients(IdNumber);
            CREATE INDEX IF NOT EXISTS IX_Patients_Status ON Patients(Status);
        ");

        // 4. 测量记录表（依赖患者、用户）
        await db.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS MeasurementRecords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PatientId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                MeasurementDate TEXT NOT NULL,
                Status INTEGER NOT NULL DEFAULT 0,
                VideoPath TEXT,
                Duration INTEGER,
                Remark TEXT,
                IsGuestData INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (PatientId) REFERENCES Patients(Id),
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            );
            CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_PatientId ON MeasurementRecords(PatientId);
            CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_MeasurementDate ON MeasurementRecords(MeasurementDate);
            CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_Status ON MeasurementRecords(Status);
        ");

        // 5. 步态参数表（依赖测量记录）
        await db.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS GaitParameters (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MeasurementId INTEGER NOT NULL UNIQUE,
                StrideLengthLeft REAL,
                StrideLengthRight REAL,
                StepLengthLeft REAL,
                StepLengthRight REAL,
                Cadence REAL,
                Velocity REAL,
                StancePhaseLeft REAL,
                StancePhaseRight REAL,
                SwingPhaseLeft REAL,
                SwingPhaseRight REAL,
                DoubleSupport REAL,
                SingleSupport REAL,
                SymmetryIndex REAL,
                ParametersJson TEXT,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (MeasurementId) REFERENCES MeasurementRecords(Id)
            );
        ");

        // 6. 报告表（依赖测量记录、患者、用户）
        await db.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS Reports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MeasurementId INTEGER NOT NULL,
                PatientId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                ReportNumber TEXT NOT NULL UNIQUE,
                ReportDate TEXT NOT NULL,
                DoctorOpinion TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                FilePath TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                FOREIGN KEY (MeasurementId) REFERENCES MeasurementRecords(Id),
                FOREIGN KEY (PatientId) REFERENCES Patients(Id),
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            );
            CREATE INDEX IF NOT EXISTS IX_Reports_ReportNumber ON Reports(ReportNumber);
            CREATE INDEX IF NOT EXISTS IX_Reports_PatientId ON Reports(PatientId);
            CREATE INDEX IF NOT EXISTS IX_Reports_ReportDate ON Reports(ReportDate);
        ");

        // 7. 系统设置表（无依赖）
        await db.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS SystemSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT,
                ValueType TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
        ");

        // 8. 操作日志表（无依赖）
        await db.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS OperationLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER,
                UserName TEXT,
                Operation TEXT NOT NULL,
                Module TEXT NOT NULL,
                Description TEXT NOT NULL,
                IpAddress TEXT,
                Level INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_OperationLogs_UserId ON OperationLogs(UserId);
            CREATE INDEX IF NOT EXISTS IX_OperationLogs_CreatedAt ON OperationLogs(CreatedAt);
            CREATE INDEX IF NOT EXISTS IX_OperationLogs_Level ON OperationLogs(Level);
        ");
    }

    /// <summary>
    /// 初始化内置数据
    /// </summary>
    private async Task SeedDataAsync(SqliteHelper db)
    {
        var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);

        // 创建默认科室
        try
        {
            _logHelper?.Information("Step 1: 创建默认科室...");
            await db.ExecuteNonQueryAsync(
                "INSERT OR IGNORE INTO Departments (Name, Phone, CreatedAt, UpdatedAt) VALUES (@Name, @Phone, @CreatedAt, @UpdatedAt)",
                new { Name = "默认科室", Phone = "", CreatedAt = now, UpdatedAt = now });
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
            await db.ExecuteNonQueryAsync(
                "INSERT OR IGNORE INTO Users (Username, PasswordHash, PasswordSalt, Name, Phone, Role, IsEnabled, IsBuiltIn, CreatedAt, UpdatedAt) VALUES (@Username, @PasswordHash, @PasswordSalt, @Name, @Phone, @Role, @IsEnabled, @IsBuiltIn, @CreatedAt, @UpdatedAt)",
                new
                {
                    Username = Constants.ADMIN_USERNAME,
                    PasswordHash = adminHash,
                    PasswordSalt = adminSalt,
                    Name = "管理员",
                    Phone = "",
                    Role = (int)UserRole.Administrator,
                    IsEnabled = 1,
                    IsBuiltIn = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            _logHelper?.Information("Step 2: 完成");
        }
        catch (Exception ex)
        {
            throw new Exception($"Step 2 失败 (admin): {ex.Message}", ex);
        }

        try
        {
            _logHelper?.Information("Step 3: 创建 user 用户...");
            await db.ExecuteNonQueryAsync(
                "INSERT OR IGNORE INTO Users (Username, PasswordHash, PasswordSalt, Name, Phone, Role, IsEnabled, IsBuiltIn, CreatedAt, UpdatedAt) VALUES (@Username, @PasswordHash, @PasswordSalt, @Name, @Phone, @Role, @IsEnabled, @IsBuiltIn, @CreatedAt, @UpdatedAt)",
                new
                {
                    Username = Constants.USER_USERNAME,
                    PasswordHash = userHash,
                    PasswordSalt = userSalt,
                    Name = "操作员",
                    Phone = "",
                    Role = (int)UserRole.Operator,
                    IsEnabled = 1,
                    IsBuiltIn = 1,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            _logHelper?.Information("Step 3: 完成");
        }
        catch (Exception ex)
        {
            throw new Exception($"Step 3 失败 (user): {ex.Message}", ex);
        }

            // 初始化系统设置
            var settings = new (string SettingKey, string SettingValue, string SettingType)[]
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
                var (settingKey, settingValue, settingType) = settings[i];
                try
                {
                    _logHelper?.Information($"Step 4.{i + 1}: 创建设置 {settingKey}...");
                    await db.ExecuteNonQueryAsync(
                        "INSERT OR IGNORE INTO SystemSettings ([Key], [Value], ValueType, UpdatedAt) VALUES (@SettingKey, @SettingValue, @SettingType, @UpdatedAt)",
                        new { SettingKey = settingKey, SettingValue = settingValue, SettingType = settingType, UpdatedAt = now });
                    _logHelper?.Information($"Step 4.{i + 1}: 完成");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Step 4.{i + 1} 失败 (设置 {settingKey}): {ex.Message}", ex);
                }
            }
        }

    /// <summary>
    /// 升级数据库
    /// </summary>
    private async Task UpgradeDatabaseAsync(SqliteHelper db, int fromVersion)
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

                // 未来版本升级在此添加
                // case 2:
                //     await UpgradeToV2Async(db);
                //     break;
            }
        }
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
