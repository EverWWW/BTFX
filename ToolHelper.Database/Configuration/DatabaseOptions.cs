using ToolHelper.Database.Abstractions;

namespace ToolHelper.Database.Configuration;

/// <summary>
/// 数据库配置基类
/// </summary>
public abstract class DatabaseOptions
{
    /// <summary>
    /// 数据库类型
    /// </summary>
    public abstract DatabaseType DatabaseType { get; }

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 命令超时时间（秒）
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// 连接超时时间（秒）
    /// </summary>
    public int ConnectionTimeout { get; set; } = 15;

    /// <summary>
    /// 是否启用连接池
    /// </summary>
    public bool EnablePooling { get; set; } = true;

    /// <summary>
    /// 最小连接池大小
    /// </summary>
    public int MinPoolSize { get; set; } = 0;

    /// <summary>
    /// 最大连接池大小
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    public int RetryIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 是否启用日志记录
    /// </summary>
    public bool EnableLogging { get; set; } = false;

    /// <summary>
    /// 批量操作大小
    /// </summary>
    public int BatchSize { get; set; } = 1000;
}

/// <summary>
/// SQLite 数据库配置
/// </summary>
public class SqliteOptions : DatabaseOptions
{
    /// <inheritdoc/>
    public override DatabaseType DatabaseType => DatabaseType.Sqlite;

    /// <summary>
    /// 数据库文件路径
    /// </summary>
    public string DatabasePath { get; set; } = "database.db";

    /// <summary>
    /// 是否使用内存数据库
    /// </summary>
    public bool InMemory { get; set; } = false;

    /// <summary>
    /// 缓存大小（页数）
    /// </summary>
    public int CacheSize { get; set; } = 2000;

    /// <summary>
    /// 日志模式
    /// </summary>
    public SqliteJournalMode JournalMode { get; set; } = SqliteJournalMode.Wal;

    /// <summary>
    /// 同步模式
    /// </summary>
    public SqliteSynchronousMode SynchronousMode { get; set; } = SqliteSynchronousMode.Normal;

    /// <summary>
    /// 是否外键约束
    /// </summary>
    public bool ForeignKeys { get; set; } = true;

    /// <summary>
    /// 密码（加密）
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 构建连接字符串
    /// </summary>
    public string BuildConnectionString()
    {
        if (InMemory)
        {
            return "Data Source=:memory:";
        }

        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate,
            Cache = Microsoft.Data.Sqlite.SqliteCacheMode.Shared,
            Pooling = EnablePooling,
            DefaultTimeout = ConnectionTimeout
        };

        if (!string.IsNullOrEmpty(Password))
        {
            builder.Password = Password;
        }

        return builder.ConnectionString;
    }
}

/// <summary>
/// SQLite 日志模式
/// </summary>
public enum SqliteJournalMode
{
    /// <summary>删除模式</summary>
    Delete,
    /// <summary>截断模式</summary>
    Truncate,
    /// <summary>持久模式</summary>
    Persist,
    /// <summary>内存模式</summary>
    Memory,
    /// <summary>WAL模式（推荐）</summary>
    Wal,
    /// <summary>关闭</summary>
    Off
}

/// <summary>
/// SQLite 同步模式
/// </summary>
public enum SqliteSynchronousMode
{
    /// <summary>关闭同步</summary>
    Off,
    /// <summary>普通同步</summary>
    Normal,
    /// <summary>完全同步</summary>
    Full,
    /// <summary>额外同步</summary>
    Extra
}

/// <summary>
/// SQL Server 数据库配置
/// </summary>
public class SqlServerOptions : DatabaseOptions
{
    /// <inheritdoc/>
    public override DatabaseType DatabaseType => DatabaseType.SqlServer;

    /// <summary>
    /// 服务器地址
    /// </summary>
    public string Server { get; set; } = "localhost";

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; } = 1433;

    /// <summary>
    /// 数据库名称
    /// </summary>
    public string Database { get; set; } = "master";

    /// <summary>
    /// 用户名
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// 密码
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// 是否使用集成安全性（Windows认证）
    /// </summary>
    public bool IntegratedSecurity { get; set; } = false;

    /// <summary>
    /// 是否加密连接
    /// </summary>
    public bool Encrypt { get; set; } = true;

    /// <summary>
    /// 是否信任服务器证书
    /// </summary>
    public bool TrustServerCertificate { get; set; } = true;

    /// <summary>
    /// 应用程序名称
    /// </summary>
    public string ApplicationName { get; set; } = "ToolHelper.Database";

    /// <summary>
    /// 多活动结果集
    /// </summary>
    public bool MultipleActiveResultSets { get; set; } = true;

    /// <summary>
    /// 构建连接字符串
    /// </summary>
    public string BuildConnectionString()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Port == 1433 ? Server : $"{Server},{Port}",
            InitialCatalog = Database,
            IntegratedSecurity = IntegratedSecurity,
            Encrypt = Encrypt,
            TrustServerCertificate = TrustServerCertificate,
            ApplicationName = ApplicationName,
            MultipleActiveResultSets = MultipleActiveResultSets,
            ConnectTimeout = ConnectionTimeout,
            CommandTimeout = CommandTimeout,
            Pooling = EnablePooling,
            MinPoolSize = MinPoolSize,
            MaxPoolSize = MaxPoolSize
        };

        if (!IntegratedSecurity)
        {
            builder.UserID = UserId;
            builder.Password = Password;
        }

        return builder.ConnectionString;
    }
}

/// <summary>
/// MySQL 数据库配置
/// </summary>
public class MySqlOptions : DatabaseOptions
{
    /// <inheritdoc/>
    public override DatabaseType DatabaseType => DatabaseType.MySql;

    /// <summary>
    /// 服务器地址
    /// </summary>
    public string Server { get; set; } = "localhost";

    /// <summary>
    /// 端口
    /// </summary>
    public int Port { get; set; } = 3306;

    /// <summary>
    /// 数据库名称
    /// </summary>
    public string Database { get; set; } = "mysql";

    /// <summary>
    /// 用户名
    /// </summary>
    public string UserId { get; set; } = "root";

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 字符集
    /// </summary>
    public string Charset { get; set; } = "utf8mb4";

    /// <summary>
    /// SSL模式
    /// </summary>
    public MySqlSslMode SslMode { get; set; } = MySqlSslMode.Preferred;

    /// <summary>
    /// 是否允许用户变量
    /// </summary>
    public bool AllowUserVariables { get; set; } = true;

    /// <summary>
    /// 是否允许加载本地文件
    /// </summary>
    public bool AllowLoadLocalInfile { get; set; } = false;

    /// <summary>
    /// 构建连接字符串
    /// </summary>
    public string BuildConnectionString()
    {
        var builder = new MySqlConnector.MySqlConnectionStringBuilder
        {
            Server = Server,
            Port = (uint)Port,
            Database = Database,
            UserID = UserId,
            Password = Password,
            CharacterSet = Charset,
            SslMode = (MySqlConnector.MySqlSslMode)SslMode,
            AllowUserVariables = AllowUserVariables,
            AllowLoadLocalInfile = AllowLoadLocalInfile,
            ConnectionTimeout = (uint)ConnectionTimeout,
            DefaultCommandTimeout = (uint)CommandTimeout,
            Pooling = EnablePooling,
            MinimumPoolSize = (uint)MinPoolSize,
            MaximumPoolSize = (uint)MaxPoolSize
        };

        return builder.ConnectionString;
    }
}

/// <summary>
/// MySQL SSL模式
/// </summary>
public enum MySqlSslMode
{
    /// <summary>禁用SSL</summary>
    None = 0,
    /// <summary>首选SSL</summary>
    Preferred = 1,
    /// <summary>必须SSL</summary>
    Required = 2,
    /// <summary>验证CA</summary>
    VerifyCA = 3,
    /// <summary>完全验证</summary>
    VerifyFull = 4
}
