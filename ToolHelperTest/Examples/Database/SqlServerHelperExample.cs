using Microsoft.Extensions.Options;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.SqlServer;

namespace ToolHelperTest.Examples.Database;

/// <summary>
/// SqlServerHelper 使用示例
/// 演示 SQL Server 数据库的基本操作
/// </summary>
/// <remarks>
/// 注意: 运行这些示例需要可用的 SQL Server 实例
/// 可以使用 Docker 快速启动:
/// docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Password" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
/// </remarks>
public class SqlServerHelperExample
{
    // 默认连接配置（请根据实际情况修改）
    private static SqlServerOptions GetDefaultOptions() => new()
    {
        Server = "localhost",
        Database = "TestDB",
        UserId = "sa",
        Password = "YourStrong@Password",
        TrustServerCertificate = true,
        EnableLogging = true
    };

    /// <summary>
    /// 示例 1: 基本CRUD操作
    /// </summary>
    public static async Task BasicCrudExample()
    {
        Console.WriteLine("=== SQL Server 基本CRUD操作示例 ===\n");
        Console.WriteLine("注意: 此示例需要可用的 SQL Server 实例\n");

        var options = Options.Create(GetDefaultOptions());

        try
        {
            using var helper = new SqlServerHelper(options);

            // 测试连接
            if (!await helper.TestConnectionAsync())
            {
                Console.WriteLine("? 无法连接到 SQL Server，跳过示例");
                Console.WriteLine("  请确保 SQL Server 正在运行并检查连接配置");
                return;
            }

            Console.WriteLine("? 连接成功");

            // 创建表
            await helper.ExecuteNonQueryAsync(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Employees' AND xtype='U')
                CREATE TABLE Employees (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(100) NOT NULL,
                    Department NVARCHAR(50),
                    Salary DECIMAL(10,2),
                    HireDate DATE DEFAULT GETDATE()
                )");
            Console.WriteLine("? 创建表 Employees");

            // 清空表
            await helper.ExecuteNonQueryAsync("DELETE FROM Employees");

            // 插入数据
            await helper.ExecuteNonQueryAsync(@"
                INSERT INTO Employees (Name, Department, Salary) VALUES 
                (@Name, @Department, @Salary)",
                new { Name = "张三", Department = "技术部", Salary = 15000.00m });

            await helper.ExecuteNonQueryAsync(@"
                INSERT INTO Employees (Name, Department, Salary) VALUES 
                (@Name, @Department, @Salary)",
                new { Name = "李四", Department = "市场部", Salary = 12000.00m });

            await helper.ExecuteNonQueryAsync(@"
                INSERT INTO Employees (Name, Department, Salary) VALUES 
                (@Name, @Department, @Salary)",
                new { Name = "王五", Department = "技术部", Salary = 18000.00m });

            Console.WriteLine("? 插入员工数据");

            // 查询
            var employees = await helper.QueryAsync<Employee>(
                "SELECT * FROM Employees ORDER BY Id");

            Console.WriteLine($"\n员工列表 ({employees.Count()} 条):");
            foreach (var emp in employees)
            {
                Console.WriteLine($"  {emp.Id}: {emp.Name} - {emp.Department} - ?{emp.Salary:N2}");
            }

            // 聚合查询
            var avgSalary = await helper.ExecuteScalarAsync<decimal>(
                "SELECT AVG(Salary) FROM Employees");
            Console.WriteLine($"\n平均工资: ?{avgSalary:N2}");

            // 按部门统计
            var deptStats = await helper.QueryAsync<DepartmentStat>(@"
                SELECT Department, COUNT(*) as EmployeeCount, AVG(Salary) as AvgSalary
                FROM Employees
                GROUP BY Department");

            Console.WriteLine("\n部门统计:");
            foreach (var stat in deptStats)
            {
                Console.WriteLine($"  {stat.Department}: {stat.EmployeeCount}人, 平均工资: ?{stat.AvgSalary:N2}");
            }

            // 清理
            await helper.ExecuteNonQueryAsync("DROP TABLE Employees");
            Console.WriteLine("\n? 清理完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 示例 2: 存储过程
    /// </summary>
    public static async Task StoredProcedureExample()
    {
        Console.WriteLine("\n=== SQL Server 存储过程示例 ===\n");

        var options = Options.Create(GetDefaultOptions());

        try
        {
            using var helper = new SqlServerHelper(options);

            if (!await helper.TestConnectionAsync())
            {
                Console.WriteLine("? 无法连接到 SQL Server，跳过示例");
                return;
            }

            // 创建表
            await helper.ExecuteNonQueryAsync(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Products' AND xtype='U')
                CREATE TABLE Products (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(100) NOT NULL,
                    Price DECIMAL(10,2) NOT NULL,
                    Category NVARCHAR(50)
                )");

            await helper.ExecuteNonQueryAsync("DELETE FROM Products");

            // 插入测试数据
            await helper.ExecuteNonQueryAsync(@"
                INSERT INTO Products (Name, Price, Category) VALUES 
                ('产品A', 100.00, '电子'),
                ('产品B', 200.00, '电子'),
                ('产品C', 150.00, '服装'),
                ('产品D', 80.00, '服装'),
                ('产品E', 300.00, '食品')");

            // 创建存储过程
            await helper.ExecuteNonQueryAsync(@"
                IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetProductsByCategory')
                    DROP PROCEDURE sp_GetProductsByCategory");

            await helper.ExecuteNonQueryAsync(@"
                CREATE PROCEDURE sp_GetProductsByCategory
                    @Category NVARCHAR(50)
                AS
                BEGIN
                    SELECT * FROM Products WHERE Category = @Category ORDER BY Price
                END");

            Console.WriteLine("? 创建存储过程 sp_GetProductsByCategory");

            // 调用存储过程
            var electronicProducts = await helper.ExecuteStoredProcedureListAsync<ProductInfo>(
                "sp_GetProductsByCategory",
                new { Category = "电子" });

            Console.WriteLine("\n电子类产品:");
            foreach (var product in electronicProducts)
            {
                Console.WriteLine($"  {product.Name}: ?{product.Price:N2}");
            }

            // 创建带输出参数的存储过程
            await helper.ExecuteNonQueryAsync(@"
                IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_GetCategoryStats')
                    DROP PROCEDURE sp_GetCategoryStats");

            await helper.ExecuteNonQueryAsync(@"
                CREATE PROCEDURE sp_GetCategoryStats
                    @Category NVARCHAR(50),
                    @TotalProducts INT OUTPUT,
                    @AvgPrice DECIMAL(10,2) OUTPUT
                AS
                BEGIN
                    SELECT @TotalProducts = COUNT(*), @AvgPrice = AVG(Price)
                    FROM Products WHERE Category = @Category
                END");

            Console.WriteLine("\n? 创建带输出参数的存储过程");

            // 调用带输出参数的存储过程
            var outputParams = await helper.ExecuteStoredProcedureWithOutputAsync(
                "sp_GetCategoryStats",
                new { Category = "电子" },
                new Dictionary<string, System.Data.SqlDbType>
                {
                    ["TotalProducts"] = System.Data.SqlDbType.Int,
                    ["AvgPrice"] = System.Data.SqlDbType.Decimal
                });

            Console.WriteLine($"\n电子类产品统计:");
            Console.WriteLine($"  产品数量: {outputParams["TotalProducts"]}");
            Console.WriteLine($"  平均价格: ?{outputParams["AvgPrice"]}");

            // 清理
            await helper.ExecuteNonQueryAsync("DROP PROCEDURE IF EXISTS sp_GetProductsByCategory");
            await helper.ExecuteNonQueryAsync("DROP PROCEDURE IF EXISTS sp_GetCategoryStats");
            await helper.ExecuteNonQueryAsync("DROP TABLE Products");
            Console.WriteLine("\n? 清理完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 示例 3: 批量操作 (SqlBulkCopy)
    /// </summary>
    public static async Task BulkOperationExample()
    {
        Console.WriteLine("\n=== SQL Server 批量操作示例 ===\n");

        var options = Options.Create(GetDefaultOptions());
        options.Value.BatchSize = 1000;

        try
        {
            using var helper = new SqlServerHelper(options);

            if (!await helper.TestConnectionAsync())
            {
                Console.WriteLine("? 无法连接到 SQL Server，跳过示例");
                return;
            }

            // 创建表
            await helper.ExecuteNonQueryAsync(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SalesData' AND xtype='U')
                CREATE TABLE SalesData (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ProductName NVARCHAR(100),
                    Quantity INT,
                    UnitPrice DECIMAL(10,2),
                    SaleDate DATE
                )");

            await helper.TruncateTableAsync("SalesData");

            // 生成测试数据
            var salesData = Enumerable.Range(1, 10000)
                .Select(i => new SalesRecord
                {
                    ProductName = $"产品_{i % 100:D3}",
                    Quantity = Random.Shared.Next(1, 100),
                    UnitPrice = Math.Round(10.0m + (decimal)Random.Shared.NextDouble() * 990.0m, 2),
                    SaleDate = DateTime.Now.AddDays(-Random.Shared.Next(0, 365))
                })
                .ToList();

            Console.WriteLine($"准备批量插入 {salesData.Count} 条记录 (使用 SqlBulkCopy)...");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var insertedCount = await helper.BulkInsertAsync("SalesData", salesData);
            sw.Stop();

            Console.WriteLine($"? 批量插入完成: {insertedCount} 条, 耗时: {sw.ElapsedMilliseconds}ms");

            // 统计
            var stats = await helper.QueryFirstOrDefaultAsync<SalesStats>(@"
                SELECT 
                    COUNT(*) as TotalRecords,
                    SUM(Quantity) as TotalQuantity,
                    SUM(Quantity * UnitPrice) as TotalSales,
                    AVG(UnitPrice) as AvgPrice
                FROM SalesData");

            Console.WriteLine($"\n销售统计:");
            Console.WriteLine($"  总记录数: {stats?.TotalRecords}");
            Console.WriteLine($"  总销量: {stats?.TotalQuantity}");
            Console.WriteLine($"  总销售额: ?{stats?.TotalSales:N2}");
            Console.WriteLine($"  平均单价: ?{stats?.AvgPrice:N2}");

            // 清理
            await helper.ExecuteNonQueryAsync("DROP TABLE SalesData");
            Console.WriteLine("\n? 清理完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 示例 4: 数据库信息查询
    /// </summary>
    public static async Task DatabaseInfoExample()
    {
        Console.WriteLine("\n=== SQL Server 数据库信息示例 ===\n");

        var options = Options.Create(GetDefaultOptions());

        try
        {
            using var helper = new SqlServerHelper(options);

            if (!await helper.TestConnectionAsync())
            {
                Console.WriteLine("? 无法连接到 SQL Server，跳过示例");
                return;
            }

            // 获取数据库信息
            var dbInfo = await helper.GetDatabaseInfoAsync();
            Console.WriteLine($"数据库信息:");
            Console.WriteLine($"  名称: {dbInfo.DatabaseName}");
            Console.WriteLine($"  数据大小: {dbInfo.DataSizeMB:N2} MB");
            Console.WriteLine($"  日志大小: {dbInfo.LogSizeMB:N2} MB");
            Console.WriteLine($"  总大小: {dbInfo.TotalSizeMB:N2} MB");

            // 获取所有表
            var tables = await helper.GetTableNamesAsync();
            Console.WriteLine($"\n数据库中的表:");
            foreach (var table in tables.Take(10))
            {
                Console.WriteLine($"  - {table}");
            }
            if (tables.Count() > 10)
            {
                Console.WriteLine($"  ... 还有 {tables.Count() - 10} 个表");
            }

            // 创建测试表查看结构
            await helper.ExecuteNonQueryAsync(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TestSchema' AND xtype='U')
                CREATE TABLE TestSchema (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(100) NOT NULL,
                    Value DECIMAL(18,4) DEFAULT 0,
                    CreatedAt DATETIME2 DEFAULT GETDATE(),
                    IsActive BIT DEFAULT 1
                )");

            // 获取表结构
            Console.WriteLine("\nTestSchema 表结构:");
            var schema = await helper.GetTableSchemaAsync("TestSchema");
            foreach (var col in schema)
            {
                var pkFlag = col.IsPrimaryKey ? " [PK]" : "";
                var nullFlag = col.IsNullable ? " NULL" : " NOT NULL";
                var identityFlag = col.IsIdentity ? " IDENTITY" : "";
                Console.WriteLine($"  {col.ColumnName}: {col.DataType}{pkFlag}{nullFlag}{identityFlag}");
            }

            // 检查表是否存在
            Console.WriteLine($"\nTestSchema 是否存在: {await helper.TableExistsAsync("TestSchema")}");
            Console.WriteLine($"NonExistent 是否存在: {await helper.TableExistsAsync("NonExistent")}");

            // 清理
            await helper.ExecuteNonQueryAsync("DROP TABLE TestSchema");
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
        Console.WriteLine("║    SQL Server Helper 示例程序            ║");
        Console.WriteLine("║    需要可用的 SQL Server 实例            ║");
        Console.WriteLine("╚══════════════════════════════════════════╝\n");

        await BasicCrudExample();
        await StoredProcedureExample();
        await BulkOperationExample();
        await DatabaseInfoExample();

        Console.WriteLine("\n========================================");
        Console.WriteLine("所有 SQL Server 示例运行完成!");
        Console.WriteLine("========================================");
    }
}

// 示例实体类
public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public DateTime HireDate { get; set; }
}

public class DepartmentStat
{
    public string Department { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public decimal AvgSalary { get; set; }
}

public class ProductInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class SalesRecord
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime SaleDate { get; set; }
}

public class SalesStats
{
    public int TotalRecords { get; set; }
    public long TotalQuantity { get; set; }
    public decimal TotalSales { get; set; }
    public decimal AvgPrice { get; set; }
}
