using SqlSugar;

namespace ToolHelper.Database.Configuration;

/// <summary>
/// SqlSugar 数据库配置选项
/// </summary>
public class SqlSugarOptions
{
    /// <summary>
    /// 数据库类型
    /// </summary>
    public DbType DbType { get; set; } = DbType.Sqlite;

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// 是否自动关闭连接
    /// </summary>
    public bool IsAutoCloseConnection { get; set; } = true;

    /// <summary>
    /// 命令超时时间（秒）
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// 是否启用 SQL 日志
    /// </summary>
    public bool EnableSqlLog { get; set; } = false;

    /// <summary>
    /// SQL 日志回调
    /// </summary>
    public Action<string, SugarParameter[]>? OnLogExecuting { get; set; }

    /// <summary>
    /// SQL 执行完成回调
    /// </summary>
    public Action<string, SugarParameter[]>? OnLogExecuted { get; set; }

    /// <summary>
    /// 错误回调
    /// </summary>
    public Action<Exception>? OnError { get; set; }

    /// <summary>
    /// 初始化数据库（建表等）
    /// </summary>
    public bool InitDatabase { get; set; } = false;

    /// <summary>
    /// 初始化时创建的实体类型
    /// </summary>
    public Type[]? InitEntityTypes { get; set; }

    /// <summary>
    /// 构建 ConnectionConfig
    /// </summary>
    /// <returns>SqlSugar 连接配置</returns>
    public ConnectionConfig ToConnectionConfig()
    {
        var config = new ConnectionConfig
        {
            DbType = DbType,
            ConnectionString = ConnectionString,
            IsAutoCloseConnection = IsAutoCloseConnection,
            InitKeyType = InitKeyType.Attribute,
            MoreSettings = new ConnMoreSettings
            {
                IsAutoRemoveDataCache = true
            }
        };

        return config;
    }
}

/// <summary>
/// SQLite SqlSugar 配置
/// </summary>
public class SqliteSugarOptions : SqlSugarOptions
{
    /// <summary>
    /// 数据库文件路径
    /// </summary>
    public string DatabasePath { get; set; } = "database.db";

    /// <summary>
    /// 是否使用内存数据库
    /// </summary>
    public bool InMemory { get; set; } = false;

    /// <summary>
    /// 创建 SqliteSugarOptions 实例
    /// </summary>
    public SqliteSugarOptions()
    {
        DbType = DbType.Sqlite;
    }

    /// <summary>
    /// 构建连接字符串
    /// </summary>
    public string BuildConnectionString()
    {
        if (InMemory)
        {
            return "DataSource=:memory:";
        }
        return $"DataSource={DatabasePath}";
    }

    /// <summary>
    /// 自动设置连接字符串
    /// </summary>
    public void AutoSetConnectionString()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            ConnectionString = BuildConnectionString();
        }
    }
}

/// <summary>
/// SQL Server SqlSugar 配置
/// </summary>
public class SqlServerSugarOptions : SqlSugarOptions
{
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
    /// 是否使用 Windows 认证
    /// </summary>
    public bool IntegratedSecurity { get; set; } = false;

    /// <summary>
    /// 是否信任服务器证书
    /// </summary>
    public bool TrustServerCertificate { get; set; } = true;

    /// <summary>
    /// 创建 SqlServerSugarOptions 实例
    /// </summary>
    public SqlServerSugarOptions()
    {
        DbType = DbType.SqlServer;
    }

    /// <summary>
    /// 构建连接字符串
    /// </summary>
    public string BuildConnectionString()
    {
        var server = Port == 1433 ? Server : $"{Server},{Port}";
        var auth = IntegratedSecurity
            ? "Integrated Security=True"
            : $"User Id={UserId};Password={Password}";

        return $"Server={server};Database={Database};{auth};TrustServerCertificate={TrustServerCertificate}";
    }

    /// <summary>
    /// 自动设置连接字符串
    /// </summary>
    public void AutoSetConnectionString()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            ConnectionString = BuildConnectionString();
        }
    }
}

/// <summary>
/// MySQL SqlSugar 配置
/// </summary>
public class MySqlSugarOptions : SqlSugarOptions
{
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
    /// 创建 MySqlSugarOptions 实例
    /// </summary>
    public MySqlSugarOptions()
    {
        DbType = DbType.MySql;
    }

    /// <summary>
    /// 构建连接字符串
    /// </summary>
    public string BuildConnectionString()
    {
        return $"Server={Server};Port={Port};Database={Database};Uid={UserId};Pwd={Password};Charset={Charset}";
    }

    /// <summary>
    /// 自动设置连接字符串
    /// </summary>
    public void AutoSetConnectionString()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            ConnectionString = BuildConnectionString();
        }
    }
}
