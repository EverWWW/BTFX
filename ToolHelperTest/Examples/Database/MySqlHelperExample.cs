using Microsoft.Extensions.Options;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.MySql;

namespace ToolHelperTest.Examples.Database;

/// <summary>
/// MySqlHelper 使用示例
/// 演示 MySQL 数据库的基本操作
/// </summary>
/// <remarks>
/// 注意: 运行这些示例需要可用的 MySQL 实例
/// 可以使用 Docker 快速启动:
/// docker run -e MYSQL_ROOT_PASSWORD=password -e MYSQL_DATABASE=testdb -p 3306:3306 -d mysql:8.0
/// </remarks>
public class MySqlHelperExample
{
    // 默认连接配置（请根据实际情况修改）
    private static MySqlOptions GetDefaultOptions() => new()
    {
        Server = "localhost",
        Port = 3306,
        Database = "testdb",
        UserId = "root",
        Password = "password",
        Charset = "utf8mb4",
        EnableLogging = true
    };

    /// <summary>
    /// 示例 1: 基本CRUD操作
    /// </summary>
    public static async Task BasicCrudExample()
    {
        Console.WriteLine("=== MySQL 基本CRUD操作示例 ===\n");
        Console.WriteLine("注意: 此示例需要可用的 MySQL 实例\n");

        var options = Options.Create(GetDefaultOptions());

        try
        {
            using var helper = new MySqlHelper(options);

            // 测试连接
            if (!await helper.TestConnectionAsync())
            {
                Console.WriteLine("? 无法连接到 MySQL，跳过示例");
                Console.WriteLine("  请确保 MySQL 正在运行并检查连接配置");
                return;
            }

            Console.WriteLine("? 连接成功");

            // 获取服务器版本
            var version = await helper.GetServerVersionAsync();
            Console.WriteLine($"  MySQL 版本: {version}");

            // 创建表
            await helper.ExecuteNonQueryAsync(@"
                CREATE TABLE IF NOT EXISTS Customers (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    Name VARCHAR(100) NOT NULL,
                    Email VARCHAR(200) UNIQUE,
                    Phone VARCHAR(20),
                    Address TEXT,
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci");
            Console.WriteLine("? 创建表 Customers");

            // 清空表
            await helper.TruncateTableAsync("Customers");

            // 插入数据并获取ID
            var id1 = await helper.InsertAndGetIdAsync(
                "INSERT INTO Customers (Name, Email, Phone) VALUES (@Name, @Email, @Phone)",
                new { Name = "张三", Email = "zhang@example.com", Phone = "13800138001" });
            Console.WriteLine($"? 插入客户: 张三, ID = {id1}");

            var id2 = await helper.InsertAndGetIdAsync(
                "INSERT INTO Customers (Name, Email, Phone) VALUES (@Name, @Email, @Phone)",
                new { Name = "李四", Email = "li@example.com", Phone = "13800138002" });
            Console.WriteLine($"? 插入客户: 李四, ID = {id2}");

            var id3 = await helper.InsertAndGetIdAsync(
                "INSERT INTO Customers (Name, Email, Phone) VALUES (@Name, @Email, @Phone)",
                new { Name = "王五", Email = "wang@example.com", Phone = "13800138003" });
            Console.WriteLine($"? 插入客户: 王五, ID = {id3}");

            // 查询
            var customers = await helper.QueryAsync<Customer>(
                "SELECT * FROM Customers ORDER BY Id");

            Console.WriteLine($"\n客户列表 ({customers.Count()} 条):");
            foreach (var customer in customers)
            {
                Console.WriteLine($"  {customer.Id}: {customer.Name} - {customer.Email} - {customer.Phone}");
            }

            // 更新
            var affected = await helper.ExecuteNonQueryAsync(
                "UPDATE Customers SET Address = @Address WHERE Id = @Id",
                new { Address = "北京市朝阳区", Id = id1 });
            Console.WriteLine($"\n? 更新 {affected} 条记录");

            // 删除
            affected = await helper.ExecuteNonQueryAsync(
                "DELETE FROM Customers WHERE Id = @Id",
                new { Id = id3 });
            Console.WriteLine($"? 删除 {affected} 条记录");

            // 验证
            var count = await helper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Customers");
            Console.WriteLine($"? 当前记录数: {count}");

            // 清理
            await helper.ExecuteNonQueryAsync("DROP TABLE IF EXISTS Customers");
            Console.WriteLine("\n? 清理完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 示例 2: INSERT ON DUPLICATE KEY UPDATE
    /// </summary>
    public static async Task InsertOrUpdateExample()
    {
        Console.WriteLine("\n=== MySQL INSERT ON DUPLICATE KEY UPDATE 示例 ===\n");

        var options = Options.Create(GetDefaultOptions());

        try
        {
            using var helper = new MySqlHelper(options);

            if (!await helper.TestConnectionAsync())
            {
                Console.WriteLine("? 无法连接到 MySQL，跳过示例");
                return;
            }

            // 创建表
            await helper.ExecuteNonQueryAsync(@"
                CREATE TABLE IF NOT EXISTS Settings (
                    `Key` VARCHAR(100) PRIMARY KEY,
                    `Value` TEXT NOT NULL,
                    Description VARCHAR(200),
                    UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                ) ENGINE=InnoDB");

            await helper.TruncateTableAsync("Settings");

            Console.WriteLine("初始插入配置...");

            // 初始插入
            var settings = new[]
            {
                new Setting { Key = "app.name", Value = "MyApp", Description = "应用名称" },
                new Setting { Key = "app.version", Value = "1.0.0", Description = "版本号" },
                new Setting { Key = "app.debug", Value = "false", Description = "调试模式" }
            };

            foreach (var setting in settings)
            {
                await helper.InsertOrUpdateAsync("Settings", setting);
                Console.WriteLine($"  {setting.Key} = {setting.Value}");
            }

            // 查询当前状态
            var currentSettings = await helper.QueryAsync<Setting>(
                "SELECT `Key`, `Value`, Description FROM Settings");

            Console.WriteLine("\n当前配置:");
            foreach (var s in currentSettings)
            {
                Console.WriteLine($"  {s.Key} = {s.Value}");
            }

            // 更新（使用 INSERT ON DUPLICATE KEY UPDATE）
            Console.WriteLine("\n更新配置...");

            var updatedSettings = new[]
            {
                new Setting { Key = "app.version", Value = "2.0.0", Description = "版本号" },
                new Setting { Key = "app.debug", Value = "true", Description = "调试模式" },
                new Setting { Key = "app.theme", Value = "dark", Description = "主题" }  // 新增
            };

            foreach (var setting in updatedSettings)
            {
                await helper.InsertOrUpdateAsync("Settings", setting);
                Console.WriteLine($"  {setting.Key} = {setting.Value}");
            }

            // 查询更新后状态
            currentSettings = await helper.QueryAsync<Setting>(
                "SELECT `Key`, `Value`, Description FROM Settings");

            Console.WriteLine("\n更新后配置:");
            foreach (var s in currentSettings)
            {
                Console.WriteLine($"  {s.Key} = {s.Value}");
            }

            // 清理
            await helper.ExecuteNonQueryAsync("DROP TABLE IF EXISTS Settings");
            Console.WriteLine("\n? 清理完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 示例 3: 批量操作
    /// </summary>
    public static async Task BulkOperationExample()
    {
        Console.WriteLine("\n=== MySQL 批量操作示例 ===\n");

        var options = Options.Create(GetDefaultOptions());
        options.Value.BatchSize = 500;

        try
        {
            using var helper = new MySqlHelper(options);

            if (!await helper.TestConnectionAsync())
            {
                Console.WriteLine("? 无法连接到 MySQL，跳过示例");
                return;
            }

            // 创建表
            await helper.ExecuteNonQueryAsync(@"
                CREATE TABLE IF NOT EXISTS Orders (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    OrderNo VARCHAR(50) NOT NULL,
                    CustomerName VARCHAR(100),
                    Amount DECIMAL(12,2),
                    Status TINYINT DEFAULT 0,
                    OrderDate DATE,
                    INDEX idx_orderno (OrderNo),
                    INDEX idx_date (OrderDate)
                ) ENGINE=InnoDB");

            await helper.TruncateTableAsync("Orders");

            // 生成测试数据
            var orders = Enumerable.Range(1, 5000)
                .Select(i => new Order
                {
                    OrderNo = $"ORD{DateTime.Now:yyyyMMdd}{i:D6}",
                    CustomerName = $"客户_{i % 100:D3}",
                    Amount = Math.Round(100.0m + (decimal)Random.Shared.NextDouble() * 9900.0m, 2),
                    Status = (byte)Random.Shared.Next(0, 4),
                    OrderDate = DateTime.Now.AddDays(-Random.Shared.Next(0, 90))
                })
                .ToList();

            Console.WriteLine($"准备批量插入 {orders.Count} 条订单...");

            // 方法1: 使用 MySqlBulkCopy
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var count = await helper.BulkInsertAsync("Orders", orders);
            sw.Stop();
            Console.WriteLine($"? MySqlBulkCopy 插入: {count} 条, 耗时: {sw.ElapsedMilliseconds}ms");

            // 统计
            var stats = await helper.QueryFirstOrDefaultAsync<OrderStats>(@"
                SELECT 
                    COUNT(*) as TotalOrders,
                    SUM(Amount) as TotalAmount,
                    AVG(Amount) as AvgAmount,
                    MIN(Amount) as MinAmount,
                    MAX(Amount) as MaxAmount
                FROM Orders");

            Console.WriteLine($"\n订单统计:");
            Console.WriteLine($"  总订单数: {stats?.TotalOrders}");
            Console.WriteLine($"  总金额: ?{stats?.TotalAmount:N2}");
            Console.WriteLine($"  平均金额: ?{stats?.AvgAmount:N2}");
            Console.WriteLine($"  最小金额: ?{stats?.MinAmount:N2}");
            Console.WriteLine($"  最大金额: ?{stats?.MaxAmount:N2}");

            // 按状态统计
            var statusStats = await helper.QueryAsync<StatusStat>(@"
                SELECT Status, COUNT(*) as Count, SUM(Amount) as TotalAmount
                FROM Orders
                GROUP BY Status
                ORDER BY Status");

            Console.WriteLine("\n按状态统计:");
            foreach (var stat in statusStats)
            {
                var statusName = stat.Status switch
                {
                    0 => "待处理",
                    1 => "处理中",
                    2 => "已完成",
                    3 => "已取消",
                    _ => "未知"
                };
                Console.WriteLine($"  {statusName}: {stat.Count} 单, ?{stat.TotalAmount:N2}");
            }

            // 清理
            await helper.ExecuteNonQueryAsync("DROP TABLE IF EXISTS Orders");
            Console.WriteLine("\n? 清理完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 示例 4: 数据库管理
    /// </summary>
    public static async Task DatabaseManagementExample()
    {
        Console.WriteLine("\n=== MySQL 数据库管理示例 ===\n");

        var options = Options.Create(GetDefaultOptions());

        try
        {
            using var helper = new MySqlHelper(options);

            if (!await helper.TestConnectionAsync())
            {
                Console.WriteLine("? 无法连接到 MySQL，跳过示例");
                return;
            }

            // 服务器信息
            var version = await helper.GetServerVersionAsync();
            Console.WriteLine($"MySQL 版本: {version}");

            var connectionCount = await helper.GetConnectionCountAsync();
            Console.WriteLine($"当前连接数: {connectionCount}");

            // 数据库信息
            var dbInfo = await helper.GetDatabaseInfoAsync();
            if (dbInfo != null)
            {
                Console.WriteLine($"\n数据库信息:");
                Console.WriteLine($"  名称: {dbInfo.DatabaseName}");
                Console.WriteLine($"  数据大小: {dbInfo.DataSizeMB:N2} MB");
                Console.WriteLine($"  索引大小: {dbInfo.IndexSizeMB:N2} MB");
                Console.WriteLine($"  总大小: {dbInfo.TotalSizeMB:N2} MB");
                Console.WriteLine($"  表数量: {dbInfo.TableCount}");
            }

            // 获取所有表
            var tables = await helper.GetTableNamesAsync();
            Console.WriteLine($"\n数据库中的表:");
            foreach (var table in tables)
            {
                Console.WriteLine($"  - {table}");
            }

            // 创建测试表
            await helper.ExecuteNonQueryAsync(@"
                CREATE TABLE IF NOT EXISTS TestMgmt (
                    Id INT AUTO_INCREMENT PRIMARY KEY COMMENT '主键',
                    Name VARCHAR(100) NOT NULL COMMENT '名称',
                    Value DECIMAL(10,2) DEFAULT 0.00 COMMENT '数值',
                    IsActive TINYINT(1) DEFAULT 1 COMMENT '是否激活',
                    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间'
                ) ENGINE=InnoDB COMMENT='测试表'");

            // 查看表结构
            Console.WriteLine("\nTestMgmt 表结构:");
            var schema = await helper.GetTableSchemaAsync("TestMgmt");
            foreach (var col in schema)
            {
                var nullFlag = col.IsNullable ? "NULL" : "NOT NULL";
                var pkFlag = col.IsPrimaryKey ? " [PK]" : "";
                var aiFlag = col.IsAutoIncrement ? " AUTO_INCREMENT" : "";
                Console.WriteLine($"  {col.ColumnName}: {col.DataType}{pkFlag} {nullFlag}{aiFlag}");
                if (!string.IsNullOrEmpty(col.Comment))
                {
                    Console.WriteLine($"    注释: {col.Comment}");
                }
            }

            // 插入测试数据
            for (int i = 1; i <= 100; i++)
            {
                await helper.ExecuteNonQueryAsync(
                    "INSERT INTO TestMgmt (Name, Value) VALUES (@Name, @Value)",
                    new { Name = $"测试_{i}", Value = Random.Shared.NextDouble() * 1000 });
            }

            // 表维护操作
            Console.WriteLine("\n执行表维护操作...");

            // 分析表
            await helper.AnalyzeTableAsync("TestMgmt");
            Console.WriteLine("? ANALYZE TABLE 完成");

            // 优化表
            await helper.OptimizeTableAsync("TestMgmt");
            Console.WriteLine("? OPTIMIZE TABLE 完成");

            // 检查表
            var checkResults = await helper.CheckTableAsync("TestMgmt");
            Console.WriteLine("? CHECK TABLE 结果:");
            foreach (var result in checkResults)
            {
                Console.WriteLine($"  {result.Msg_type}: {result.Msg_text}");
            }

            // 创建/删除索引
            await helper.CreateIndexAsync("TestMgmt", "idx_name", ["Name"]);
            Console.WriteLine("? 创建索引 idx_name");

            await helper.DropIndexAsync("TestMgmt", "idx_name");
            Console.WriteLine("? 删除索引 idx_name");

            // 清理
            await helper.ExecuteNonQueryAsync("DROP TABLE IF EXISTS TestMgmt");
            Console.WriteLine("\n? 清理完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 运行所有示例
    /// </summary>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║       MySQL Helper 示例程序              ║");
        Console.WriteLine("║       需要可用的 MySQL 实例              ║");
        Console.WriteLine("╚══════════════════════════════════════════╝\n");

        await BasicCrudExample();
        await InsertOrUpdateExample();
        await BulkOperationExample();
        await DatabaseManagementExample();

        Console.WriteLine("\n========================================");
        Console.WriteLine("所有 MySQL 示例运行完成!");
        Console.WriteLine("========================================");
    }
}

// 示例实体类
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Address { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public byte Status { get; set; }
    public DateTime OrderDate { get; set; }
}

public class OrderStats
{
    public int TotalOrders { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AvgAmount { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
}

public class StatusStat
{
    public byte Status { get; set; }
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}
