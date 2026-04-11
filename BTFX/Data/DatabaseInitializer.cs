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
/// ���ݿ��ʼ����
/// ���𴴽����ݿ⡢���ṹ�ͳ�ʼ����
/// ʹ�� SqlSugar ORM �������ݿ����
/// </summary>
public class DatabaseInitializer
{
    /// <summary>
    /// ��ǰ���ݿ�汾
    /// �汾��ʷ:
    /// - 1: ��ʼ�汾
    /// - 2: ��������ģ����չ������ MeasurementRecord �ֶΣ�MeasurementName, MeasurementType, FrontVideoPath, SideVideoPath �ȣ�
    /// </summary>
    public const int CurrentDatabaseVersion = 3;

    private readonly string _databasePath;
    private readonly ILogHelper? _logHelper;

    /// <summary>
    /// ���캯��
    /// </summary>
    /// <param name="logHelper">��־���֣���ѡ��</param>
    public DatabaseInitializer(ILogHelper? logHelper = null)
    {
        _logHelper = logHelper;

        // ���ݿ�·��
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dbDir = Path.Combine(baseDir, Constants.DATABASE_DIRECTORY);

        // ȷ��Ŀ¼����
        if (!Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        _databasePath = Path.Combine(dbDir, Constants.DATABASE_FILENAME);
    }

    /// <summary>
    /// ��ȡ���ݿ�·��
    /// </summary>
    public string DatabasePath => _databasePath;

    /// <summary>
    /// ���� SqliteSugarHelper ʵ��
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
    /// ��ʼ�����ݿ�
    /// </summary>
    public async Task InitializeAsync()
    {
        _logHelper?.Information("��ʼ��ʼ�����ݿ�...");

        Exception? firstException = null;

        try
        {
            await InitializeDatabaseCoreAsync();
            return; // �ɹ���ֱ�ӷ���
        }
        catch (Exception ex)
        {
            firstException = ex;
            _logHelper?.Warning($"���ݿ��ʼ��ʧ�ܣ�׼���ؽ����ݿ�: {ex.Message}");
        }

        // ǿ�ƽ����������գ��ͷ��������ݿ�����
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // �ȴ�һС��ʱ�����ļ������ȫ�ͷ�
        await Task.Delay(500);

        // ����ɾ�����ݿⲢ���³�ʼ��
        try
        {
            DeleteDatabaseSafe();
            _logHelper?.Information("��ɾ�������ݿ⣬���³�ʼ��...");

            await InitializeDatabaseCoreAsync();
            _logHelper?.Information("���ݿ��ؽ��ɹ�");
        }
        catch (Exception retryEx)
        {
            // ������ϸ�Ĵ�����Ϣ
            var fullMessage = retryEx.Message;
            var inner = retryEx.InnerException;
            while (inner != null)
            {
                fullMessage += $"\n�ڲ��쳣: {inner.Message}";
                inner = inner.InnerException;
            }

            _logHelper?.Error($"���ݿ��ؽ�ʧ��: {fullMessage}\n��ջ: {retryEx.StackTrace}", retryEx);

            // ����ؽ�Ҳʧ�ܣ��׳�ԭʼ�쳣�����м�ֵ��
            var originalMessage = firstException?.Message ?? retryEx.Message;
            throw new Exception($"���ݿ��ʼ��ʧ��: {originalMessage}", firstException ?? retryEx);
        }
    }

    /// <summary>
    /// ��ȫɾ�����ݿ⣨�����ļ���ռ�õ������
    /// </summary>
    private void DeleteDatabaseSafe()
    {
        // ���� SQLite ���ӳ�
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
                    // �ļ���ռ�ã��ȴ�������
                    Thread.Sleep(200 * (retry + 1));
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }
    }

    /// <summary>
    /// ���ݿ��ʼ�������߼�
    /// </summary>
    private async Task InitializeDatabaseCoreAsync()
    {
        using var db = CreateSqliteSugarHelper();

        // ������ݿ�汾
        var version = GetDatabaseVersion(db);
        _logHelper?.Information($"��ǰ���ݿ�汾: {version}");

        if (version == 0)
        {
            // �����ݿ⣬�������б��ͳ�ʼ����
            _logHelper?.Information("�������ݿ���ṹ...");
            await CreateTablesAsync(db);

            _logHelper?.Information("��ʼ����������...");
            await SeedDataAsync(db);
            
            SetDatabaseVersion(db, CurrentDatabaseVersion);
            _logHelper?.Information($"���ݿ��ʼ����ɣ��汾: {CurrentDatabaseVersion}");
        }
        else if (version < CurrentDatabaseVersion)
        {
            // ��Ҫ����
            _logHelper?.Information($"�������ݿ�: {version} -> {CurrentDatabaseVersion}");
            await UpgradeDatabaseAsync(db, version);
            SetDatabaseVersion(db, CurrentDatabaseVersion);
            _logHelper?.Information("���ݿ��������");
        }
        else
        {
            _logHelper?.Information("���ݿ��������°汾");
        }
    }

    /// <summary>
    /// ��ȡ���ݿ�汾
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
    /// �������ݿ�汾
    /// </summary>
    private void SetDatabaseVersion(SqliteSugarHelper db, int version)
    {
        db.ExecuteSql($"PRAGMA user_version = {version};");
    }

    /// <summary>
    /// �������б���ʹ�� CodeFirst��
    /// </summary>
    private Task CreateTablesAsync(SqliteSugarHelper db)
    {
        _logHelper?.Information("ʹ�� CodeFirst �������ṹ...");

        // ������˳�򴴽���
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

        // ����������SqlSugar CodeFirst ���Զ�������������Ҫ�ֶ�ִ�У�
        CreateIndexes(db);

        return Task.CompletedTask;
    }

    /// <summary>
    /// ��������
    /// </summary>
    private void CreateIndexes(SqliteSugarHelper db)
    {
        _logHelper?.Information("�������ݿ�����...");

        // Users ������
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Users_Username ON Users(Username);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Users_Phone ON Users(Phone);");

        // Patients ������
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_Name ON Patients(Name);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_Phone ON Patients(Phone);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_IdNumber ON Patients(IdNumber);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Patients_Status ON Patients(Status);");

        // MeasurementRecords ������
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_PatientId ON MeasurementRecords(PatientId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_MeasurementDate ON MeasurementRecords(MeasurementDate);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_MeasurementRecords_Status ON MeasurementRecords(Status);");

        // Reports ������
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Reports_ReportNumber ON Reports(ReportNumber);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Reports_PatientId ON Reports(PatientId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_Reports_ReportDate ON Reports(ReportDate);");

        // AnalysisResults ������
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_MeasurementId ON AnalysisResults(MeasurementId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_RequestId ON AnalysisResults(RequestId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_Success ON AnalysisResults(Success);");

        // KinematicSummaries ������
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_KinematicSummaries_AnalysisResultId ON KinematicSummaries(AnalysisResultId);");

        // AnalysisCsvFiles ������
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisCsvFiles_AnalysisResultId ON AnalysisCsvFiles(AnalysisResultId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisCsvFiles_FileType ON AnalysisCsvFiles(FileType);");

        // QualityControls ������
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_QualityControls_AnalysisResultId ON QualityControls(AnalysisResultId);");
    }

    /// <summary>
    /// ��ʼ����������
    /// </summary>
    private async Task SeedDataAsync(SqliteSugarHelper db)
    {
        var now = DateTime.Now;

        // ����Ĭ�Ͽ���
        try
        {
            _logHelper?.Information("Step 1: ����Ĭ�Ͽ���...");
            
            // ����Ƿ��Ѵ���
            if (!db.Any<Department>(d => d.Name == "Ĭ�Ͽ���"))
            {
                await db.InsertAsync(new Department
                {
                    Name = "Ĭ�Ͽ���",
                    Phone = "",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            _logHelper?.Information("Step 1: ���");
        }
        catch (Exception ex)
        {
            throw new Exception($"Step 1 ʧ�� (Departments): {ex.Message}", ex);
        }

        // ���������û�
        var adminSalt = PasswordHelper.GenerateSalt();
        var adminHash = PasswordHelper.HashPassword(Constants.DEFAULT_PASSWORD, adminSalt);
        var userSalt = PasswordHelper.GenerateSalt();
        var userHash = PasswordHelper.HashPassword(Constants.DEFAULT_PASSWORD, userSalt);

        try
        {
            _logHelper?.Information("Step 2: ���� admin �û�...");
            
            if (!await db.AnyAsync<User>(u => u.Username == Constants.ADMIN_USERNAME))
            {
                await db.InsertAsync(new User
                {
                    Username = Constants.ADMIN_USERNAME,
                    PasswordHash = adminHash,
                    PasswordSalt = adminSalt,
                    Name = "����Ա",
                    Phone = "",
                    Role = UserRole.Administrator,
                    IsEnabled = true,
                    IsBuiltIn = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            _logHelper?.Information("Step 2: ���");
        }
        catch (Exception ex)
        {
            throw new Exception($"Step 2 ʧ�� (admin): {ex.Message}", ex);
        }

        try
        {
            _logHelper?.Information("Step 3: ���� user �û�...");
            
            if (!await db.AnyAsync<User>(u => u.Username == Constants.USER_USERNAME))
            {
                await db.InsertAsync(new User
                {
                    Username = Constants.USER_USERNAME,
                    PasswordHash = userHash,
                    PasswordSalt = userSalt,
                    Name = "����Ա",
                    Phone = "",
                    Role = UserRole.Operator,
                    IsEnabled = true,
                    IsBuiltIn = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            _logHelper?.Information("Step 3: ���");
        }
        catch (Exception ex)
        {
            throw new Exception($"Step 3 ʧ�� (user): {ex.Message}", ex);
        }

        // ��ʼ��ϵͳ����
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
                _logHelper?.Information($"Step 4.{i + 1}: �������� {key}...");
                
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
                _logHelper?.Information($"Step 4.{i + 1}: ���");
            }
            catch (Exception ex)
            {
                throw new Exception($"Step 4.{i + 1} ʧ�� (���� {key}): {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// �������ݿ�
    /// </summary>
    private Task UpgradeDatabaseAsync(SqliteSugarHelper db, int fromVersion)
    {
        // ���汾��ִ�������ű�
        for (int version = fromVersion + 1; version <= CurrentDatabaseVersion; version++)
        {
            _logHelper?.Information($"ִ�������ű� v{version}...");

            switch (version)
            {
                case 1:
                    // v0 -> v1: ��ʼ�汾���������ű�
                    break;

                case 2:
                    // v1 -> v2: ��������ģ����չ
                    UpgradeToV2(db);
                    break;

                case 3:
                    // v2 -> v3: ����ģ�飨������������� + GaitParameters ��չ�ֶΣ�
                    UpgradeToV3(db);
                    break;

                // δ���汾�����ڴ�����
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// ������ v2: ��������ģ����չ�ֶ�
    /// </summary>
    private void UpgradeToV2(SqliteSugarHelper db)
    {
        _logHelper?.Information("������ v2: ���Ӳ�������ģ���ֶ�...");

        // ʹ�� CodeFirst �Զ�ͬ�����ṹ��SqlSugar ���Զ�����ȱʧ���У�
        db.CreateTables(typeof(MeasurementRecord));

        // Ҳ�����ֶ������У���� CodeFirst ����Ч��
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
                // �п����Ѵ��ڣ����Դ���
                _logHelper?.Debug($"�п����Ѵ���: {ex.Message}");
            }
        }

        _logHelper?.Information("v2 �������");
    }

    /// <summary>
    /// ������ v3: ����ģ�� �� ����4�ŷ����� + GaitParameters ��չ�ֶ�
    /// </summary>
    private void UpgradeToV3(SqliteSugarHelper db)
    {
        _logHelper?.Information("������ v3: ���ӷ���ģ������ֶ�...");

        // ʹ�� CodeFirst �����±�
        db.CreateTables(
            typeof(AnalysisResult),
            typeof(KinematicSummary),
            typeof(AnalysisCsvFile),
            typeof(QualityControlInfo)
        );

        // GaitParameters �������ֶΣ�ALTER TABLE ���ף���ֹ CodeFirst δ��Ч��
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
                // �п����Ѵ��ڣ�����
                _logHelper?.Debug($"�п����Ѵ���: {ex.Message}");
            }
        }

        // �����±�����
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_MeasurementId ON AnalysisResults(MeasurementId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_RequestId ON AnalysisResults(RequestId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisResults_Success ON AnalysisResults(Success);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_KinematicSummaries_AnalysisResultId ON KinematicSummaries(AnalysisResultId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisCsvFiles_AnalysisResultId ON AnalysisCsvFiles(AnalysisResultId);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_AnalysisCsvFiles_FileType ON AnalysisCsvFiles(FileType);");
        db.ExecuteSql("CREATE INDEX IF NOT EXISTS IX_QualityControls_AnalysisResultId ON QualityControls(AnalysisResultId);");

        _logHelper?.Information("v3 �������");
    }

    /// <summary>
    /// ������ݿ��Ƿ����
    /// </summary>
    public bool DatabaseExists()
    {
        return File.Exists(_databasePath);
    }

    /// <summary>
    /// ɾ�����ݿ⣨�����ڲ��ԣ�
    /// </summary>
    public void DeleteDatabase()
    {
        // �������ӳ�
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }

        // ͬʱɾ�� WAL �� SHM �ļ�
        var walPath = _databasePath + "-wal";
        var shmPath = _databasePath + "-shm";

        if (File.Exists(walPath))
            File.Delete(walPath);
        if (File.Exists(shmPath))
            File.Delete(shmPath);
    }
}
