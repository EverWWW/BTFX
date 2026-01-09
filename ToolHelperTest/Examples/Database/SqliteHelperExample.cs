using Microsoft.Extensions.Options;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Sqlite;

namespace ToolHelperTest.Examples.Database;

/// <summary>
/// SqliteHelper 使用示例
/// 演示 SQLite 数据库的基本操作
/// </summary>
public class SqliteHelperExample
{
    /// <summary>
    /// 示例 1: 基本CRUD操作
    /// </summary>
    public static async Task BasicCrudExample()
    {
        Console.WriteLine("=== SQLite 基本CRUD操作示例 ===\n");

        // 创建配置
        var options = Options.Create(new SqliteOptions
        {
            DatabasePath = "example_basic.db",
            JournalMode = SqliteJournalMode.Wal,
            EnableLogging = true
        });

        // 创建帮助类
        using var helper = new SqliteHelper(options);

        // 初始化数据库（设置PRAGMA）
        await helper.InitializeAsync();
        Console.WriteLine("? 数据库初始化完成");

        // 创建表
        await helper.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Email TEXT UNIQUE,
                Age INTEGER,
                CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
            )");
        Console.WriteLine("? 创建表 Users");

        // 插入数据
        var insertedId = await helper.InsertAndGetIdAsync(
            "INSERT INTO Users (Name, Email, Age) VALUES (@Name, @Email, @Age)",
            new { Name = "张三", Email = "zhang@example.com", Age = 25 });
        Console.WriteLine($"? 插入用户: 张三, ID = {insertedId}");

        await helper.InsertAndGetIdAsync(
            "INSERT INTO Users (Name, Email, Age) VALUES (@Name, @Email, @Age)",
            new { Name = "李四", Email = "li@example.com", Age = 30 });
        Console.WriteLine("? 插入用户: 李四");

        await helper.InsertAndGetIdAsync(
            "INSERT INTO Users (Name, Email, Age) VALUES (@Name, @Email, @Age)",
            new { Name = "王五", Email = "wang@example.com", Age = 28 });
        Console.WriteLine("? 插入用户: 王五");

        // 查询单条记录
        var user = await helper.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Id = @Id",
            new { Id = insertedId });
        Console.WriteLine($"\n查询到用户: {user?.Name}, Email: {user?.Email}, Age: {user?.Age}");

        // 查询多条记录
        var users = await helper.QueryAsync<User>("SELECT * FROM Users ORDER BY Id");
        Console.WriteLine($"\n所有用户 ({users.Count()} 条):");
        foreach (var u in users)
        {
            Console.WriteLine($"  - {u.Id}: {u.Name} ({u.Email})");
        }

        // 更新数据
        var affected = await helper.ExecuteNonQueryAsync(
            "UPDATE Users SET Age = @Age WHERE Id = @Id",
            new { Age = 26, Id = insertedId });
        Console.WriteLine($"\n? 更新 {affected} 条记录");

        // 删除数据
        affected = await helper.ExecuteNonQueryAsync(
            "DELETE FROM Users WHERE Id = @Id",
            new { Id = insertedId });
        Console.WriteLine($"? 删除 {affected} 条记录");

        // 统计记录数
        var count = await helper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Users");
        Console.WriteLine($"? 当前记录数: {count}");

        // 清理
        await helper.ExecuteNonQueryAsync("DROP TABLE IF EXISTS Users");
        Console.WriteLine("\n? 清理完成");
    }

    /// <summary>
    /// 示例 2: 事务操作
    /// </summary>
    public static async Task TransactionExample()
    {
        Console.WriteLine("\n=== SQLite 事务操作示例 ===\n");

        var options = Options.Create(new SqliteOptions
        {
            DatabasePath = "example_transaction.db"
        });

        using var helper = new SqliteHelper(options);
        await helper.InitializeAsync();

        // 创建表
        await helper.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS Accounts (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL,
                Balance REAL NOT NULL
            )");

        // 初始化账户
        await helper.ExecuteNonQueryAsync("DELETE FROM Accounts");
        await helper.ExecuteNonQueryAsync(
            "INSERT INTO Accounts (Id, Name, Balance) VALUES (1, '账户A', 1000.00), (2, '账户B', 500.00)");

        Console.WriteLine("初始余额:");
        var accounts = await helper.QueryAsync<Account>("SELECT * FROM Accounts");
        foreach (var acc in accounts)
        {
            Console.WriteLine($"  {acc.Name}: {acc.Balance:N2}");
        }

        // 执行转账事务
        Console.WriteLine("\n执行转账: 账户A -> 账户B, 金额: 200.00");

        try
        {
            await helper.ExecuteInTransactionAsync(async transaction =>
            {
                // 扣款
                await helper.ExecuteNonQueryAsync(
                    "UPDATE Accounts SET Balance = Balance - 200 WHERE Id = 1");

                // 模拟检查余额
                var balance = await helper.ExecuteScalarAsync<double>(
                    "SELECT Balance FROM Accounts WHERE Id = 1");

                if (balance < 0)
                {
                    throw new InvalidOperationException("余额不足!");
                }

                // 入账
                await helper.ExecuteNonQueryAsync(
                    "UPDATE Accounts SET Balance = Balance + 200 WHERE Id = 2");
            });

            Console.WriteLine("? 转账成功!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 转账失败: {ex.Message}");
        }

        Console.WriteLine("\n转账后余额:");
        accounts = await helper.QueryAsync<Account>("SELECT * FROM Accounts");
        foreach (var acc in accounts)
        {
            Console.WriteLine($"  {acc.Name}: {acc.Balance:N2}");
        }

        // 测试事务回滚
        Console.WriteLine("\n尝试转账超出余额的金额: 账户A -> 账户B, 金额: 10000.00");

        try
        {
            await helper.ExecuteInTransactionAsync(async transaction =>
            {
                await helper.ExecuteNonQueryAsync(
                    "UPDATE Accounts SET Balance = Balance - 10000 WHERE Id = 1");

                var balance = await helper.ExecuteScalarAsync<double>(
                    "SELECT Balance FROM Accounts WHERE Id = 1");

                if (balance < 0)
                {
                    throw new InvalidOperationException("余额不足!");
                }

                await helper.ExecuteNonQueryAsync(
                    "UPDATE Accounts SET Balance = Balance + 10000 WHERE Id = 2");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 转账失败，事务已回滚: {ex.Message}");
        }

        Console.WriteLine("\n余额保持不变:");
        accounts = await helper.QueryAsync<Account>("SELECT * FROM Accounts");
        foreach (var acc in accounts)
        {
            Console.WriteLine($"  {acc.Name}: {acc.Balance:N2}");
        }

        // 清理
        await helper.ExecuteNonQueryAsync("DROP TABLE IF EXISTS Accounts");
    }

    /// <summary>
    /// 示例 3: 批量操作
    /// </summary>
    public static async Task BulkOperationExample()
    {
        Console.WriteLine("\n=== SQLite 批量操作示例 ===\n");

        var options = Options.Create(new SqliteOptions
        {
            DatabasePath = "example_bulk.db",
            BatchSize = 500
        });

        using var helper = new SqliteHelper(options);
        await helper.InitializeAsync();

        // 创建表
        await helper.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS Products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                Stock INTEGER NOT NULL
            )");

        await helper.ExecuteNonQueryAsync("DELETE FROM Products");

        // 生成测试数据
        var products = Enumerable.Range(1, 1000)
            .Select(i => new Product
            {
                Name = $"产品_{i:D4}",
                Price = Math.Round(10.0 + Random.Shared.NextDouble() * 990.0, 2),
                Stock = Random.Shared.Next(0, 1000)
            })
            .ToList();

        Console.WriteLine($"准备批量插入 {products.Count} 条记录...");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var insertedCount = await helper.BulkInsertAsync("Products", products);
        sw.Stop();

        Console.WriteLine($"? 批量插入完成: {insertedCount} 条, 耗时: {sw.ElapsedMilliseconds}ms");

        // 验证
        var totalCount = await helper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM Products");
        var avgPrice = await helper.ExecuteScalarAsync<double>("SELECT AVG(Price) FROM Products");
        var totalStock = await helper.ExecuteScalarAsync<long>("SELECT SUM(Stock) FROM Products");

        Console.WriteLine($"\n统计信息:");
        Console.WriteLine($"  总记录数: {totalCount}");
        Console.WriteLine($"  平均价格: {avgPrice:N2}");
        Console.WriteLine($"  总库存: {totalStock}");

        // 清理
        await helper.ExecuteNonQueryAsync("DROP TABLE IF EXISTS Products");
    }

    /// <summary>
    /// 示例 4: 数据库管理
    /// </summary>
    public static async Task DatabaseManagementExample()
    {
        Console.WriteLine("\n=== SQLite 数据库管理示例 ===\n");

            var options = Options.Create(new SqliteOptions
            {
                DatabasePath = "example_management.db"
            });

            var backupPath = "example_management_backup.db";

            using (var helper = new SqliteHelper(options))
            {
                await helper.InitializeAsync();

                // 创建一些表
                await helper.ExecuteNonQueryAsync(@"
                    CREATE TABLE IF NOT EXISTS TestTable1 (Id INTEGER PRIMARY KEY, Data TEXT);
                    CREATE TABLE IF NOT EXISTS TestTable2 (Id INTEGER PRIMARY KEY, Value REAL);
                    CREATE TABLE IF NOT EXISTS TestTable3 (Id INTEGER PRIMARY KEY, Name TEXT);
                ");

                // 获取所有表名
                var tables = await helper.GetTableNamesAsync();
                Console.WriteLine("数据库中的表:");
                foreach (var table in tables)
                {
                    Console.WriteLine($"  - {table}");
                }

                // 检查表是否存在
                var exists = await helper.TableExistsAsync("TestTable1");
                Console.WriteLine($"\nTestTable1 是否存在: {exists}");

                exists = await helper.TableExistsAsync("NonExistentTable");
                Console.WriteLine($"NonExistentTable 是否存在: {exists}");

                // 获取表结构
                Console.WriteLine("\nTestTable1 表结构:");
                var schema = await helper.GetTableSchemaAsync("TestTable1");
                foreach (var col in schema)
                {
                    Console.WriteLine($"  {col.name} ({col.type}), PK: {col.pk == 1}, NotNull: {col.notnull == 1}");
                }

                // 创建索引
                await helper.ExecuteNonQueryAsync("INSERT INTO TestTable1 (Data) VALUES ('Test1'), ('Test2'), ('Test3')");
                await helper.CreateIndexAsync("TestTable1", "idx_data", ["Data"]);
                Console.WriteLine("\n? 创建索引 idx_data");

                // 获取数据库大小
                var dbSize = await helper.GetDatabaseSizeAsync();
                Console.WriteLine($"\n数据库大小: {dbSize / 1024.0:N2} KB");

                // 执行完整性检查
                Console.WriteLine("\n执行完整性检查:");
                var checkResults = await helper.IntegrityCheckAsync();
                foreach (var result in checkResults)
                {
                    Console.WriteLine($"  {result}");
                }

                // 备份数据库
                await helper.BackupToFileAsync(backupPath);
                Console.WriteLine($"\n? 数据库已备份到: {backupPath}");

                // VACUUM
                await helper.VacuumAsync();
                Console.WriteLine("? 执行VACUUM完成");

                // 清理
                await helper.ExecuteNonQueryAsync("DROP TABLE IF EXISTS TestTable1");
                await helper.ExecuteNonQueryAsync("DROP TABLE IF EXISTS TestTable2");
                await helper.ExecuteNonQueryAsync("DROP TABLE IF EXISTS TestTable3");
            }

            // 等待连接完全关闭
            await Task.Delay(100);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // 删除备份文件
            try
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? 清理备份文件失败: {ex.Message}");
            }
        }

    /// <summary>
    /// 示例 5: 流式查询（大数据量）
    /// </summary>
    public static async Task StreamQueryExample()
    {
        Console.WriteLine("\n=== SQLite 流式查询示例 ===\n");

        var options = Options.Create(new SqliteOptions
        {
            DatabasePath = "example_stream.db"
        });

        using var helper = new SqliteHelper(options);
        await helper.InitializeAsync();

        // 创建表并插入测试数据
        await helper.ExecuteNonQueryAsync(@"
            CREATE TABLE IF NOT EXISTS LargeData (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Value TEXT NOT NULL
            )");

        await helper.ExecuteNonQueryAsync("DELETE FROM LargeData");

        // 插入测试数据
        var data = Enumerable.Range(1, 100)
            .Select(i => new { Value = $"数据项_{i:D4}" })
            .ToList();

        foreach (var item in data)
        {
            await helper.ExecuteNonQueryAsync(
                "INSERT INTO LargeData (Value) VALUES (@Value)", item);
        }

        Console.WriteLine("使用流式查询逐条处理数据:");

        int processedCount = 0;
        await foreach (var item in helper.QueryStreamAsync<DataItem>("SELECT * FROM LargeData ORDER BY Id"))
        {
            processedCount++;
            if (processedCount <= 5 || processedCount > 95)
            {
                Console.WriteLine($"  处理: {item.Id} - {item.Value}");
            }
            else if (processedCount == 6)
            {
                Console.WriteLine("  ...(省略中间数据)...");
            }
        }

        Console.WriteLine($"\n? 流式处理完成，共处理 {processedCount} 条记录");

        // 清理
        await helper.ExecuteNonQueryAsync("DROP TABLE IF EXISTS LargeData");
    }

    /// <summary>
    /// 运行所有示例
    /// </summary>
    public static async Task RunAllExamples()
    {
        try
        {
            await BasicCrudExample();
            await TransactionExample();
            await BulkOperationExample();
            await DatabaseManagementExample();
            await StreamQueryExample();

            Console.WriteLine("\n========================================");
            Console.WriteLine("所有 SQLite 示例运行完成!");
            Console.WriteLine("========================================");
        }
            finally
            {
                // 清理测试数据库文件
                await CleanupTestDatabases();
            }
        }

        private static async Task CleanupTestDatabases()
        {
            // 等待连接完全关闭
            await Task.Delay(100);
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // 清理 SQLite 连接池
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            var dbFiles = new[] 
            { 
                "example_basic.db", "example_transaction.db", 
                "example_bulk.db", "example_management.db", 
                "example_stream.db" 
            };

            foreach (var file in dbFiles)
            {
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                    if (File.Exists(file + "-wal")) File.Delete(file + "-wal");
                    if (File.Exists(file + "-shm")) File.Delete(file + "-shm");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? 清理文件 {file} 失败: {ex.Message}");
                }
            }
        }
    }

// 示例实体类
public class User
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public class Account
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Balance { get; set; }
}

public class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Price { get; set; }
    public int Stock { get; set; }
}

public class DataItem
{
    public long Id { get; set; }
    public string Value { get; set; } = string.Empty;
}
