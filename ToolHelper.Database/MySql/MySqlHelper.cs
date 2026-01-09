using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;
using ToolHelper.Database.Abstractions;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Core;

namespace ToolHelper.Database.MySql;

/// <summary>
/// MySQL 数据库帮助类
/// 提供MySQL数据库的操作封装
/// </summary>
/// <example>
/// <code>
/// // 创建MySQL帮助类
/// var options = Options.Create(new MySqlOptions
/// {
///     Server = "localhost",
///     Port = 3306,
///     Database = "mydb",
///     UserId = "root",
///     Password = "password"
/// });
/// 
/// using var helper = new MySqlHelper(options);
/// 
/// // 创建表
/// await helper.ExecuteNonQueryAsync(@"
///     CREATE TABLE IF NOT EXISTS Users (
///         Id INT AUTO_INCREMENT PRIMARY KEY,
///         Name VARCHAR(100) NOT NULL,
///         Email VARCHAR(200) UNIQUE,
///         CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
///     ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
/// 
/// // 插入数据
/// var id = await helper.InsertAndGetIdAsync(
///     "INSERT INTO Users (Name, Email) VALUES (@Name, @Email)",
///     new { Name = "张三", Email = "zhang@example.com" });
/// </code>
/// </example>
public class MySqlHelper : DbHelperBase
{
    private readonly MySqlOptions _mysqlOptions;

    /// <inheritdoc/>
    public override DatabaseType DatabaseType => DatabaseType.MySql;

    /// <inheritdoc/>
    protected override string ParameterPrefix => "@";

    /// <summary>
    /// 创建 MySqlHelper 实例
    /// </summary>
    /// <param name="options">MySQL配置选项</param>
    /// <param name="logger">日志记录器（可选）</param>
    public MySqlHelper(IOptions<MySqlOptions> options, ILogger<MySqlHelper>? logger = null)
        : base(CreateOptionsWithConnectionString(options.Value), logger)
    {
        _mysqlOptions = options.Value;
    }

    /// <summary>
    /// 使用连接字符串创建 MySqlHelper 实例
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="logger">日志记录器（可选）</param>
    public MySqlHelper(string connectionString, ILogger<MySqlHelper>? logger = null)
        : base(new MySqlOptions { ConnectionString = connectionString }, logger)
    {
        _mysqlOptions = new MySqlOptions { ConnectionString = connectionString };
    }

    private static MySqlOptions CreateOptionsWithConnectionString(MySqlOptions options)
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
        return new MySqlConnection(ConnectionString);
    }

    #region MySQL 特有功能

    /// <summary>
    /// 获取最后插入的自增ID
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>最后插入的ID</returns>
    public async Task<long> GetLastInsertIdAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteScalarAsync<long>("SELECT LAST_INSERT_ID();", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 插入数据并返回自增ID
    /// </summary>
    /// <param name="sql">INSERT SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>自增ID</returns>
    public async Task<long> InsertAndGetIdAsync(
        string sql,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        return await GetLastInsertIdAsync(cancellationToken);
    }

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
        var connection = (MySqlConnection)await GetConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = _mysqlOptions.CommandTimeout;

        if (parameters != null)
        {
            AddParameters(command, parameters);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 执行存储过程（返回结果）
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="procedureName">存储过程名称</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果集合</returns>
    public async Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(
        string procedureName,
        object? parameters = null,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        var connection = (MySqlConnection)await GetConnectionAsync(cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = procedureName;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandTimeout = _mysqlOptions.CommandTimeout;

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
    /// 批量插入（使用MySqlBulkCopy）
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

        var connection = (MySqlConnection)await GetConnectionAsync(cancellationToken);
        var dataTable = ToDataTable(entityList);

        var bulkCopy = new MySqlBulkCopy(connection)
        {
            DestinationTableName = tableName,
            BulkCopyTimeout = _mysqlOptions.CommandTimeout
        };

        // 映射列
        for (int i = 0; i < dataTable.Columns.Count; i++)
        {
            bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dataTable.Columns[i].ColumnName));
        }

        var result = await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
        return result.RowsInserted;
    }

    /// <summary>
    /// 使用 LOAD DATA INFILE 批量导入（高性能）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="tableName">表名</param>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>插入行数</returns>
    public async Task<int> BulkInsertWithLoadDataAsync<T>(
        string tableName,
        IEnumerable<T> entities,
        CancellationToken cancellationToken = default) where T : class
    {
        var entityList = entities.ToList();
        if (entityList.Count == 0) return 0;

        var properties = GetCachedProperties(typeof(T));
        var columns = string.Join(", ", properties.Select(p => $"`{p.Name}`"));

        // 使用INSERT语句批量插入
        var connection = (MySqlConnection)await GetConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var totalInserted = 0;

            // 分批处理
            foreach (var batch in entityList.Chunk(_mysqlOptions.BatchSize))
            {
                var sb = new StringBuilder();
                sb.AppendLine($"INSERT INTO `{tableName}` ({columns}) VALUES");

                var values = new List<string>();
                var paramIndex = 0;
                var allParams = new Dictionary<string, object?>();

                foreach (var entity in batch)
                {
                    var rowParams = new List<string>();
                    foreach (var property in properties)
                    {
                        var paramName = $"@p{paramIndex++}";
                        rowParams.Add(paramName);
                        allParams[paramName] = property.GetValue(entity);
                    }
                    values.Add($"({string.Join(", ", rowParams)})");
                }

                sb.AppendLine(string.Join(",\n", values));

                using var command = connection.CreateCommand();
                command.CommandText = sb.ToString();
                command.Transaction = transaction;

                foreach (var (key, value) in allParams)
                {
                    var param = command.CreateParameter();
                    param.ParameterName = key;
                    param.Value = value ?? DBNull.Value;
                    command.Parameters.Add(param);
                }

                totalInserted += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return totalInserted;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// INSERT ... ON DUPLICATE KEY UPDATE (插入或更新)
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="tableName">表名</param>
    /// <param name="entity">实体</param>
    /// <param name="updateColumns">需要更新的列（为空时更新所有非主键列）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>受影响的行数</returns>
    public async Task<int> InsertOrUpdateAsync<T>(
        string tableName,
        T entity,
        string[]? updateColumns = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var properties = GetCachedProperties(typeof(T));
        var columns = string.Join(", ", properties.Select(p => $"`{p.Name}`"));
        var values = string.Join(", ", properties.Select(p => $"@{p.Name}"));

        var updateSet = updateColumns?.Length > 0
            ? string.Join(", ", updateColumns.Select(c => $"`{c}` = VALUES(`{c}`)"))
            : string.Join(", ", properties.Select(p => $"`{p.Name}` = VALUES(`{p.Name}`)"));

        var sql = $@"INSERT INTO `{tableName}` ({columns}) VALUES ({values})
                     ON DUPLICATE KEY UPDATE {updateSet}";

        return await ExecuteNonQueryAsync(sql, entity, cancellationToken);
    }

    /// <summary>
    /// 检查表是否存在
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    public async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteScalarAsync<long>(
            @"SELECT COUNT(*) FROM information_schema.TABLES 
              WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName",
            new { TableName = tableName },
            cancellationToken);

        return result > 0;
    }

    /// <summary>
    /// 获取所有表名
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>表名列表</returns>
    public async Task<IEnumerable<string>> GetTableNamesAsync(CancellationToken cancellationToken = default)
    {
        var tables = await QueryAsync<TableNameInfo>(
            "SELECT TABLE_NAME AS TableName FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME",
            cancellationToken: cancellationToken);

        return tables.Select(t => t.TableName);
    }

    /// <summary>
    /// 获取表结构信息
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>列信息</returns>
    public async Task<IEnumerable<MySqlColumnInfo>> GetTableSchemaAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT 
                COLUMN_NAME AS ColumnName,
                DATA_TYPE AS DataType,
                CHARACTER_MAXIMUM_LENGTH AS MaxLength,
                NUMERIC_PRECISION AS NumericPrecision,
                NUMERIC_SCALE AS NumericScale,
                IS_NULLABLE = 'YES' AS IsNullable,
                COLUMN_KEY = 'PRI' AS IsPrimaryKey,
                EXTRA LIKE '%auto_increment%' AS IsAutoIncrement,
                COLUMN_DEFAULT AS DefaultValue,
                COLUMN_COMMENT AS Comment
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @TableName
            ORDER BY ORDINAL_POSITION";

        return await QueryAsync<MySqlColumnInfo>(sql, new { TableName = tableName }, cancellationToken);
    }

    /// <summary>
    /// 获取数据库大小信息
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据库大小信息</returns>
    public async Task<MySqlDatabaseInfo?> GetDatabaseInfoAsync(CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT 
                DATABASE() AS DatabaseName,
                SUM(data_length) / 1024 / 1024 AS DataSizeMB,
                SUM(index_length) / 1024 / 1024 AS IndexSizeMB,
                SUM(data_length + index_length) / 1024 / 1024 AS TotalSizeMB,
                COUNT(*) AS TableCount
            FROM information_schema.TABLES 
            WHERE TABLE_SCHEMA = DATABASE()";

        return await QueryFirstOrDefaultAsync<MySqlDatabaseInfo>(sql, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 获取服务器版本
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>版本字符串</returns>
    public async Task<string?> GetServerVersionAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteScalarAsync<string>("SELECT VERSION();", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 获取当前连接数
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>连接数</returns>
    public async Task<int> GetConnectionCountAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM information_schema.PROCESSLIST;",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 截断表
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task TruncateTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync($"TRUNCATE TABLE `{tableName}`", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 优化表
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task OptimizeTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync($"OPTIMIZE TABLE `{tableName}`", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 分析表
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task AnalyzeTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync($"ANALYZE TABLE `{tableName}`", cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 检查表
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>检查结果</returns>
    public async Task<IEnumerable<MySqlCheckResult>> CheckTableAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        return await QueryAsync<MySqlCheckResult>(
            $"CHECK TABLE `{tableName}`",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 创建索引
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="indexName">索引名</param>
    /// <param name="columns">列名</param>
    /// <param name="isUnique">是否唯一索引</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task CreateIndexAsync(
        string tableName,
        string indexName,
        string[] columns,
        bool isUnique = false,
        CancellationToken cancellationToken = default)
    {
        var uniqueKeyword = isUnique ? "UNIQUE " : "";
        var columnList = string.Join(", ", columns.Select(c => $"`{c}`"));

        await ExecuteNonQueryAsync(
            $"CREATE {uniqueKeyword}INDEX `{indexName}` ON `{tableName}` ({columnList})",
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 删除索引
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <param name="indexName">索引名</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task DropIndexAsync(
        string tableName,
        string indexName,
        CancellationToken cancellationToken = default)
    {
        await ExecuteNonQueryAsync(
            $"DROP INDEX `{indexName}` ON `{tableName}`",
            cancellationToken: cancellationToken);
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
/// 表名信息
/// </summary>
internal class TableNameInfo
{
    public string TableName { get; set; } = string.Empty;
}

/// <summary>
/// MySQL 列信息
/// </summary>
public class MySqlColumnInfo
{
    /// <summary>列名</summary>
    public string ColumnName { get; set; } = string.Empty;
    /// <summary>数据类型</summary>
    public string DataType { get; set; } = string.Empty;
    /// <summary>最大长度</summary>
    public long? MaxLength { get; set; }
    /// <summary>数字精度</summary>
    public int? NumericPrecision { get; set; }
    /// <summary>数字小数位数</summary>
    public int? NumericScale { get; set; }
    /// <summary>是否允许为空</summary>
    public bool IsNullable { get; set; }
    /// <summary>是否主键</summary>
    public bool IsPrimaryKey { get; set; }
    /// <summary>是否自增</summary>
    public bool IsAutoIncrement { get; set; }
    /// <summary>默认值</summary>
    public string? DefaultValue { get; set; }
    /// <summary>注释</summary>
    public string? Comment { get; set; }
}

/// <summary>
/// MySQL 数据库信息
/// </summary>
public class MySqlDatabaseInfo
{
    /// <summary>数据库名称</summary>
    public string DatabaseName { get; set; } = string.Empty;
    /// <summary>数据大小(MB)</summary>
    public decimal DataSizeMB { get; set; }
    /// <summary>索引大小(MB)</summary>
    public decimal IndexSizeMB { get; set; }
    /// <summary>总大小(MB)</summary>
    public decimal TotalSizeMB { get; set; }
    /// <summary>表数量</summary>
    public int TableCount { get; set; }
}

/// <summary>
/// MySQL 检查结果
/// </summary>
public class MySqlCheckResult
{
    /// <summary>表名</summary>
    public string Table { get; set; } = string.Empty;
    /// <summary>操作类型</summary>
    public string Op { get; set; } = string.Empty;
    /// <summary>消息类型</summary>
    public string Msg_type { get; set; } = string.Empty;
    /// <summary>消息内容</summary>
    public string Msg_text { get; set; } = string.Empty;
}
