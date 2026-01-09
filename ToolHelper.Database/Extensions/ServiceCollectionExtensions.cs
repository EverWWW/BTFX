using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ToolHelper.Database.Abstractions;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Core;
using ToolHelper.Database.MySql;
using ToolHelper.Database.Sqlite;
using ToolHelper.Database.SqlServer;

namespace ToolHelper.Database.Extensions;

/// <summary>
/// 服务集合扩展方法
/// </summary>
public static class ServiceCollectionExtensions
{
    #region SqlSugar ORM (推荐)

    /// <summary>
    /// 添加 SqlSugar SQLite 数据库支持（推荐）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddSqliteSugar(options =>
    /// {
    ///     options.DatabasePath = "mydata.db";
    ///     options.EnableSqlLog = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSqliteSugar(
        this IServiceCollection services,
        Action<SqliteSugarOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<SqliteSugarHelper>();
        services.TryAddSingleton<ISqlSugarDbHelper>(sp => sp.GetRequiredService<SqliteSugarHelper>());

        return services;
    }

    /// <summary>
    /// 添加 SqlSugar SQLite 数据库支持（使用数据库路径）
    /// </summary>
    public static IServiceCollection AddSqliteSugar(
        this IServiceCollection services,
        string databasePath)
    {
        return services.AddSqliteSugar(options => options.DatabasePath = databasePath);
    }

    /// <summary>
    /// 添加 SqlSugar SQL Server 数据库支持（推荐）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddSqlServerSugar(options =>
    /// {
    ///     options.Server = "localhost";
    ///     options.Database = "MyDatabase";
    ///     options.UserId = "sa";
    ///     options.Password = "YourPassword";
    ///     options.EnableSqlLog = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSqlServerSugar(
        this IServiceCollection services,
        Action<SqlServerSugarOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<SqlServerSugarHelper>();
        services.TryAddSingleton<ISqlSugarDbHelper>(sp => sp.GetRequiredService<SqlServerSugarHelper>());

        return services;
    }

    /// <summary>
    /// 添加 SqlSugar SQL Server 数据库支持（使用连接字符串）
    /// </summary>
    public static IServiceCollection AddSqlServerSugar(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddSqlServerSugar(options => options.ConnectionString = connectionString);
    }

    /// <summary>
    /// 添加 SqlSugar MySQL 数据库支持（推荐）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddMySqlSugar(options =>
    /// {
    ///     options.Server = "localhost";
    ///     options.Port = 3306;
    ///     options.Database = "mydb";
    ///     options.UserId = "root";
    ///     options.Password = "password";
    ///     options.EnableSqlLog = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMySqlSugar(
        this IServiceCollection services,
        Action<MySqlSugarOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<MySqlSugarHelper>();
        services.TryAddSingleton<ISqlSugarDbHelper>(sp => sp.GetRequiredService<MySqlSugarHelper>());

        return services;
    }

    /// <summary>
    /// 添加 SqlSugar MySQL 数据库支持（使用连接字符串）
    /// </summary>
    public static IServiceCollection AddMySqlSugar(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddMySqlSugar(options => options.ConnectionString = connectionString);
    }

    /// <summary>
    /// 添加 SqlSugar 数据库帮助类工厂
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddSqlSugarFactory();
    /// 
    /// // 使用工厂
    /// var factory = serviceProvider.GetRequiredService&lt;ISqlSugarDbHelperFactory&gt;();
    /// var sqliteHelper = factory.CreateSqlite("mydata.db");
    /// </code>
    /// </example>
    public static IServiceCollection AddSqlSugarFactory(this IServiceCollection services)
    {
        services.TryAddSingleton<ISqlSugarDbHelperFactory, SqlSugarDbHelperFactory>();
        return services;
    }

    #endregion

    #region SQLite (旧版兼容)

    /// <summary>
    /// 添加 SQLite 数据库支持（旧版，建议使用 AddSqliteSugar）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddSqlite(options =>
    /// {
    ///     options.DatabasePath = "mydata.db";
    ///     options.JournalMode = SqliteJournalMode.Wal;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSqlite(
        this IServiceCollection services,
        Action<SqliteOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<SqliteHelper>();
        services.TryAddSingleton<IDbHelper>(sp => sp.GetRequiredService<SqliteHelper>());

        return services;
    }

    /// <summary>
    /// 添加 SQLite 数据库支持（使用连接字符串）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSqlite(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddSqlite(options => options.ConnectionString = connectionString);
    }

    /// <summary>
    /// 添加命名的 SQLite 数据库支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="name">名称</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSqlite(
        this IServiceCollection services,
        string name,
        Action<SqliteOptions> configure)
    {
        services.Configure(name, configure);
        return services;
    }

    #endregion

    #region SQL Server (旧版兼容)

    /// <summary>
    /// 添加 SQL Server 数据库支持（旧版，建议使用 AddSqlServerSugar）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddSqlServer(options =>
    /// {
    ///     options.Server = "localhost";
    ///     options.Database = "MyDatabase";
    ///     options.UserId = "sa";
    ///     options.Password = "YourPassword";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSqlServer(
        this IServiceCollection services,
        Action<SqlServerOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<SqlServerHelper>();
        services.TryAddSingleton<IDbHelper>(sp => sp.GetRequiredService<SqlServerHelper>());

        return services;
    }

    /// <summary>
    /// 添加 SQL Server 数据库支持（使用连接字符串）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSqlServer(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddSqlServer(options => options.ConnectionString = connectionString);
    }

    /// <summary>
    /// 添加命名的 SQL Server 数据库支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="name">名称</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddSqlServer(
        this IServiceCollection services,
        string name,
        Action<SqlServerOptions> configure)
    {
        services.Configure(name, configure);
        return services;
    }

    #endregion

    #region MySQL (旧版兼容)

    /// <summary>
    /// 添加 MySQL 数据库支持（旧版，建议使用 AddMySqlSugar）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddMySql(options =>
    /// {
    ///     options.Server = "localhost";
    ///     options.Port = 3306;
    ///     options.Database = "mydb";
    ///     options.UserId = "root";
    ///     options.Password = "password";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMySql(
        this IServiceCollection services,
        Action<MySqlOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<MySqlHelper>();
        services.TryAddSingleton<IDbHelper>(sp => sp.GetRequiredService<MySqlHelper>());

        return services;
    }

    /// <summary>
    /// 添加 MySQL 数据库支持（使用连接字符串）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMySql(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddMySql(options => options.ConnectionString = connectionString);
    }

    /// <summary>
    /// 添加命名的 MySQL 数据库支持
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="name">名称</param>
    /// <param name="configure">配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddMySql(
        this IServiceCollection services,
        string name,
        Action<MySqlOptions> configure)
    {
        services.Configure(name, configure);
        return services;
    }

    #endregion

    #region 多数据库支持 (旧版兼容)

    /// <summary>
    /// 添加数据库帮助类工厂（旧版，建议使用 AddSqlSugarFactory）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <returns>服务集合</returns>
    /// <example>
    /// <code>
    /// services.AddDatabaseFactory();
    /// 
    /// // 使用工厂
    /// var factory = serviceProvider.GetRequiredService&lt;IDbHelperFactory&gt;();
    /// var sqliteHelper = factory.Create(DatabaseType.Sqlite, "Data Source=test.db");
    /// </code>
    /// </example>
    public static IServiceCollection AddDatabaseFactory(this IServiceCollection services)
    {
        services.TryAddSingleton<IDbHelperFactory, DbHelperFactory>();
        return services;
    }

    #endregion
}

/// <summary>
/// 数据库帮助类工厂接口
/// </summary>
public interface IDbHelperFactory
{
    /// <summary>
    /// 创建数据库帮助类
    /// </summary>
    /// <param name="type">数据库类型</param>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>数据库帮助类</returns>
    IDbHelper Create(DatabaseType type, string connectionString);

    /// <summary>
    /// 创建SQLite帮助类
    /// </summary>
    /// <param name="databasePath">数据库路径</param>
    /// <returns>SQLite帮助类</returns>
    SqliteHelper CreateSqlite(string databasePath);

    /// <summary>
    /// 创建SQL Server帮助类
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>SQL Server帮助类</returns>
    SqlServerHelper CreateSqlServer(string connectionString);

    /// <summary>
    /// 创建MySQL帮助类
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <returns>MySQL帮助类</returns>
    MySqlHelper CreateMySql(string connectionString);
}

/// <summary>
/// 数据库帮助类工厂实现
/// </summary>
internal class DbHelperFactory : IDbHelperFactory
{
    /// <inheritdoc/>
    public IDbHelper Create(DatabaseType type, string connectionString)
    {
        return type switch
        {
            DatabaseType.Sqlite => new SqliteHelper(connectionString),
            DatabaseType.SqlServer => new SqlServerHelper(connectionString),
            DatabaseType.MySql => new MySqlHelper(connectionString),
            _ => throw new NotSupportedException($"不支持的数据库类型: {type}")
        };
    }

    /// <inheritdoc/>
    public SqliteHelper CreateSqlite(string databasePath)
    {
        return new SqliteHelper($"Data Source={databasePath}");
    }

    /// <inheritdoc/>
    public SqlServerHelper CreateSqlServer(string connectionString)
    {
        return new SqlServerHelper(connectionString);
    }

        /// <inheritdoc/>
        public MySqlHelper CreateMySql(string connectionString)
        {
            return new MySqlHelper(connectionString);
        }
    }

    /// <summary>
    /// SqlSugar 数据库帮助类工厂接口
    /// </summary>
    public interface ISqlSugarDbHelperFactory
    {
        /// <summary>
        /// 创建 SQLite 帮助类
        /// </summary>
        /// <param name="databasePath">数据库文件路径</param>
        /// <returns>SQLite 帮助类</returns>
        SqliteSugarHelper CreateSqlite(string databasePath);

        /// <summary>
        /// 创建 SQLite 帮助类（使用选项）
        /// </summary>
        /// <param name="options">配置选项</param>
        /// <returns>SQLite 帮助类</returns>
        SqliteSugarHelper CreateSqlite(SqliteSugarOptions options);

        /// <summary>
        /// 创建 SQL Server 帮助类
        /// </summary>
        /// <param name="connectionString">连接字符串</param>
        /// <returns>SQL Server 帮助类</returns>
        SqlServerSugarHelper CreateSqlServer(string connectionString);

        /// <summary>
        /// 创建 SQL Server 帮助类（使用选项）
        /// </summary>
        /// <param name="options">配置选项</param>
        /// <returns>SQL Server 帮助类</returns>
        SqlServerSugarHelper CreateSqlServer(SqlServerSugarOptions options);

        /// <summary>
        /// 创建 MySQL 帮助类
        /// </summary>
        /// <param name="connectionString">连接字符串</param>
        /// <returns>MySQL 帮助类</returns>
        MySqlSugarHelper CreateMySql(string connectionString);

        /// <summary>
        /// 创建 MySQL 帮助类（使用选项）
        /// </summary>
        /// <param name="options">配置选项</param>
        /// <returns>MySQL 帮助类</returns>
        MySqlSugarHelper CreateMySql(MySqlSugarOptions options);
    }

    /// <summary>
    /// SqlSugar 数据库帮助类工厂实现
    /// </summary>
    internal class SqlSugarDbHelperFactory : ISqlSugarDbHelperFactory
    {
        /// <inheritdoc/>
        public SqliteSugarHelper CreateSqlite(string databasePath)
        {
            return new SqliteSugarHelper(databasePath);
        }

        /// <inheritdoc/>
        public SqliteSugarHelper CreateSqlite(SqliteSugarOptions options)
        {
            return new SqliteSugarHelper(options);
        }

        /// <inheritdoc/>
        public SqlServerSugarHelper CreateSqlServer(string connectionString)
        {
            return new SqlServerSugarHelper(connectionString);
        }

        /// <inheritdoc/>
        public SqlServerSugarHelper CreateSqlServer(SqlServerSugarOptions options)
        {
            return new SqlServerSugarHelper(options);
        }

        /// <inheritdoc/>
        public MySqlSugarHelper CreateMySql(string connectionString)
        {
            return new MySqlSugarHelper(connectionString);
        }

        /// <inheritdoc/>
        public MySqlSugarHelper CreateMySql(MySqlSugarOptions options)
        {
            return new MySqlSugarHelper(options);
        }
    }
