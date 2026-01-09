using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolHelper.Database.Abstractions;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Core;

namespace ToolHelper.Database.SqlServer;

/// <summary>
/// SQL Server 数据库帮助类
/// 提供SQL Server数据库的操作封装
/// </summary>
/// <example>
/// <code>
/// // 创建SQL Server帮助类
/// var options = Options.Create(new SqlServerOptions
/// {
///     Server = "localhost",
///     Database = "MyDatabase",
///     UserId = "sa",
///     Password = "YourPassword",
///     TrustServerCertificate = true
/// });
/// 
/// using var helper = new SqlServerHelper(options);
/// 
/// // 测试连接
/// if (await helper.TestConnectionAsync())
/// {
///     Console.WriteLine("连接成功!");
/// }
/// 
/// // 执行存储过程
/// var result = await helper.ExecuteStoredProcedureAsync&lt;User&gt;(
///     "sp_GetUserById",
///     new { UserId = 1 });
/// </code>
/// </example>
public class SqlServerHelper : DbHelperBase
{
    private readonly SqlServerOptions _sqlServerOptions;

    /// <inheritdoc/>
    public override DatabaseType DatabaseType => DatabaseType.SqlServer;

    /// <inheritdoc/>
    protected override string ParameterPrefix => "@";

    /// <summary>
    /// 创建 SqlServerHelper 实例
    /// </summary>
    /// <param name="options">SQL Server配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
    public SqlServerHelper(IOptions<SqlServerOptions> options, ILogger<SqlServerHelper>? logger = null)
        : base(CreateOptionsWithConnectionString(options.Value), logger)
    {
        _sqlServerOptions = options.Value;
    }

    /// <summary>
    /// 使用连接字符串创建 SqlServerHelper 实例
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="logger">日志记录器（可选）</param>
    public SqlServerHelper(string connectionString, ILogger<SqlServerHelper>? logger = null)
        : base(new SqlServerOptions { ConnectionString = connectionString }, logger)
    {
        _sqlServerOptions = new SqlServerOptions { ConnectionString = connectionString };
    }

    private static SqlServerOptions CreateOptionsWithConnectionString(SqlServerOptions options)
    {
        if (string.IsNullOrEmpty(options.ConnectionString))
        {
            options.ConnectionString = options.BuildConnectionString();
        }
        return options;
    }

    /// <inheritdoc/>
    protected override DbConnection CreateConnection()
    {
        return new SqlConnection(ConnectionString);
    }

    #region SQL Server 特有功能

    /// <summary>
    /// 执行存储过程（无返回值）
    /// </summary>
    /// <param name="procedureName">存储过程名称</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> ExecuteStoredProcedureAsync(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var connection = (SqlConnection)await GetConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = _sqlServerOptions.CommandTimeout;

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 执行存储过程（返回单条记录）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="procedureName">存储过程名称</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果</returns>
    public async Task<T?> ExecuteStoredProcedureAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        var connection = (SqlConnection)await GetConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = _sqlServerOptions.CommandTimeout;

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return MapToEntity<T>(reader);
        }

        return null;
    }

    /// <summary>
    /// 执行存储过程（返回多条记录）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="procedureName">存储过程名称</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果集合</returns>
    public async Task<IEnumerable<T>> ExecuteStoredProcedureListAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        var connection = (SqlConnection)await GetConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = _sqlServerOptions.CommandTimeout;

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        var results = new List<T>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapToEntity<T>(reader));
        }

        return results;
    }

    /// <summary>
    /// 执行存储过程（带输出参数）
    /// </summary>
    /// <param name="procedureName">存储过程名称</param>
    /// <param name="inputParameters">输入参数</param>
    /// <param name="outputParameters">输出参数定义</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>输出参数值字典</returns>
    public async Task<Dictionary<string, object?>> ExecuteStoredProcedureWithOutputAsync(
        string procedureName,
        object? inputParameters,
        Dictionary<string, SqlDbType> outputParameters,
        CancellationToken cancellationToken = default)
    {
        var connection = (SqlConnection)await GetConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = _sqlServerOptions.CommandTimeout;

        // 添加输入参数
        if (inputParameters != null)
        {
            AddParameters(command, inputParameters);
        }

        // 添加输出参数
        foreach (var (name, type) in outputParameters)
        {
            var param = command.CreateParameter();
            param.ParameterName = $"@{name}";
            param.SqlDbType = type;
            param.Direction = ParameterDirection.Output;
            command.Parameters.Add(param);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);

        // 收集输出参数值
        var result = new Dictionary<string, object?>();
        foreach (var name in outputParameters.Keys)
        {
            result[name] = command.Parameters[$"@{name}"].Value;
        }

        return result;
    }

    /// <summary>
    /// 批量插入（使用SqlBulkCopy）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="tableName">表名</param>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>插入行数</returns>
    public override async Task<int> BulkInsertAsync<T>(
        string tableName,
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0) return 0;

        var dataTable = ToDataTable(entityList);

        var connection = (SqlConnection)await GetConnectionAsync(cancellationToken);
        using var bulkCopy = new SqlBulkCopy(connection)
        {
            DestinationTableName = tableName,
            BatchSize = _sqlServerOptions.BatchSize,
            BulkCopyTimeout = _sqlServerOptions.CommandTimeout
        };

        // 映射列
        foreach (DataColumn column in dataTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
        return entityList.Count;
    }

    /// <summary>
    /// 批量合并（MERGE语句）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="tableName">表名</param>
    /// <param name="entities">实体集合</param>
    /// <param name="keyColumns">主键列</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> BulkMergeAsync<T>(
        string tableName,
        IEnumerable<T> entities,
        string[] keyColumns,
        CancellationToken cancellationToken = default) where T : class
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0) return 0;

        var properties = GetCachedProperties(typeof(T));
        var keySet = new HashSet<string>(keyColumns, StringComparer.OrdinalIgnoreCase);

        // 创建临时表名
        var tempTableName = $"#Temp_{tableName}_{Guid.NewGuid():N}";

        // 构建创建临时表SQL
        var createTempTableSql = $"SELECT TOP 0 * INTO {tempTableName} FROM {tableName}";

        // 构建MERGE语句
        var onClause = string.Join(" AND ", keyColumns.Select(k => $"Target.{k} = Source.{k}"));
        var updateClause = string.Join(", ", properties.Where(p => !keySet.Contains(p.Name))
            .Select(p => $"Target.{p.Name} = Source.{p.Name}"));
        var insertColumns = string.Join(", ", properties.Select(p => p.Name));
        var insertValues = string.Join(", ", properties.Select(p => $"Source.{p.Name}"));

        var mergeSql = $@"
            MERGE INTO {tableName} AS Target
            USING {tempTableName} AS Source
            ON {onClause}
            WHEN MATCHED THEN
                UPDATE SET {updateClause}
            WHEN NOT MATCHED THEN
                INSERT ({insertColumns}) VALUES ({insertValues});";

        var connection = (SqlConnection)await GetConnectionAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await BeginTransactionAsync(
            IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            // 创建临时表
            using var createCommand = connection.CreateCommand();
            createCommand.CommandText = createTempTableSql;
            createCommand.Transaction = transaction;
            await createCommand.ExecuteNonQueryAsync(cancellationToken);

            // 批量插入到临时表
            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction)
            {
                DestinationTableName = tempTableName,
                BatchSize = _sqlServerOptions.BatchSize
            };

            var dataTable = ToDataTable(entityList);
            foreach (DataColumn column in dataTable.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);

            // 执行MERGE
            using var mergeCommand = connection.CreateCommand();
            mergeCommand.CommandText = mergeSql;
            mergeCommand.Transaction = transaction;
            var result = await mergeCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 检查表是否存在
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="schema">架构名（默认dbo）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public async Task<bool> TableExistsAsync(
        string tableName,
        string schema = "dbo",
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
              WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName",
            new { Schema = schema, TableName = tableName },
            cancellationToken);

        return result > 0;
    }

    /// <summary>
    /// 获取所有表名
    /// </summary>
    /// <param name="schema">架构名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表名列表</returns>
    public async Task<IEnumerable<string>> GetTableNamesAsync(
        string? schema = null,
        CancellationToken cancellationToken = default)
    {
        var sql = @"SELECT TABLE_NAME AS Name FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_TYPE = 'BASE TABLE'";

        if (!string.IsNullOrEmpty(schema))
        {
            sql += " AND TABLE_SCHEMA = @Schema";
        }

        sql += " ORDER BY TABLE_NAME";

        var tables = await QueryAsync<TableInfo>(sql, new { Schema = schema }, cancellationToken);
        return tables.Select(t => t.Name);
    }

    /// <summary>
    /// 获取表结构信息
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="schema">架构名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>列信息</returns>
    public async Task<IEnumerable<SqlServerColumnInfo>> GetTableSchemaAsync(
        string tableName,
        string schema = "dbo",
        CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT 
                c.COLUMN_NAME AS ColumnName,
                c.DATA_TYPE AS DataType,
                c.CHARACTER_MAXIMUM_LENGTH AS MaxLength,
                c.NUMERIC_PRECISION AS NumericPrecision,
                c.NUMERIC_SCALE AS NumericScale,
                CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS IsNullable,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey,
                COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IsIdentity,
                c.COLUMN_DEFAULT AS DefaultValue
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                AND c.TABLE_NAME = pk.TABLE_NAME 
                AND c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @Schema AND c.TABLE_NAME = @TableName
            ORDER BY c.ORDINAL_POSITION";

        return await QueryAsync<SqlServerColumnInfo>(sql, new { Schema = schema, TableName = tableName }, cancellationToken);
    }

    /// <summary>
    /// 获取数据库大小信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据库大小信息</returns>
    public async Task<SqlServerDatabaseInfo> GetDatabaseInfoAsync(CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT 
                DB_NAME() AS DatabaseName,
                SUM(CASE WHEN type = 0 THEN size END) * 8 / 1024.0 AS DataSizeMB,
                SUM(CASE WHEN type = 1 THEN size END) * 8 / 1024.0 AS LogSizeMB,
                SUM(size) * 8 / 1024.0 AS TotalSizeMB
            FROM sys.database_files";

        return (await QueryFirstOrDefaultAsync<SqlServerDatabaseInfo>(sql, cancellationToken: cancellationToken))!;
    }

    /// <summary>
    /// 截断表
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task TruncateTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync($"TRUNCATE TABLE {tableName}", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 执行带重试的操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="operation">操作</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>操作结果</returns>
    public async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var retryCount = 0;
        var maxRetries = _sqlServerOptions.RetryCount;
        var retryInterval = _sqlServerOptions.RetryIntervalMs;

        while (true)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (SqlException ex) when (IsTransientError(ex) && retryCount < maxRetries)
            {
                retryCount++;
                LogDebug($"SQL Server 瞬态错误，重试 {retryCount}/{maxRetries}");
                await Task.Delay(retryInterval * retryCount, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 判断是否为瞬态错误
    /// </summary>
    private static bool IsTransientError(SqlException ex)
    {
        // 常见的瞬态错误码
        var transientErrorNumbers = new[] { 4060, 40197, 40501, 40613, 49918, 49919, 49920, 4221 };
        return transientErrorNumbers.Contains(ex.Number);
    }

    /// <summary>
    /// 将实体列表转换为DataTable
    /// </summary>
    private DataTable ToDataTable<T>(IEnumerable<T> entities)
    {
        var properties = GetCachedProperties(typeof(T));
        var dataTable = new DataTable();

        foreach (var property in properties)
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            dataTable.Columns.Add(property.Name, type);
        }

        foreach (var entity in entities)
        {
            var row = dataTable.NewRow();
            foreach (var property in properties)
            {
                row[property.Name] = property.GetValue(entity) ?? DBNull.Value;
            }
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    #endregion
}

/// <summary>
/// 表信息
/// </summary>
internal class TableInfo
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// SQL Server 列信息
/// </summary>
public class SqlServerColumnInfo
{
    /// <summary>列名</summary>
    public string ColumnName { get; set; } = string.Empty;
    /// <summary>数据类型</summary>
    public string DataType { get; set; } = string.Empty;
    /// <summary>最大长度</summary>
    public int? MaxLength { get; set; }
    /// <summary>数字精度</summary>
    public int? NumericPrecision { get; set; }
    /// <summary>数字小数位数</summary>
    public int? NumericScale { get; set; }
    /// <summary>是否允许为空</summary>
    public bool IsNullable { get; set; }
    /// <summary>是否主键</summary>
    public bool IsPrimaryKey { get; set; }
    /// <summary>是否自增</summary>
    public bool IsIdentity { get; set; }
    /// <summary>默认值</summary>
    public string? DefaultValue { get; set; }
}

/// <summary>
/// SQL Server 数据库信息
/// </summary>
public class SqlServerDatabaseInfo
{
    /// <summary>数据库名称</summary>
    public string DatabaseName { get; set; } = string.Empty;
    /// <summary>数据文件大小(MB)</summary>
    public decimal DataSizeMB { get; set; }
    /// <summary>日志文件大小(MB)</summary>
    public decimal LogSizeMB { get; set; }
    /// <summary>总大小(MB)</summary>
    public decimal TotalSizeMB { get; set; }
}
