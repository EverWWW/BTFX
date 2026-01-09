using Microsoft.Extensions.DependencyInjection;
using ToolHelper.Database.Abstractions;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Extensions;

namespace ToolHelperTest.Examples.Database;

/// <summary>
/// Database 模块示例运行器
/// </summary>
public class DatabaseDemoRunner
{
    /// <summary>
    /// 运行所有数据库示例
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         ToolHelper.Database 模块示例                     ║");
        Console.WriteLine("║    SQLite / SQL Server / MySQL 操作示例                  ║");
        Console.WriteLine("║    新增: SqlSugar ORM 支持（推荐）                       ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("\n请选择要运行的示例:");
            Console.WriteLine("=== SqlSugar ORM (推荐，无需写SQL) ===");
            Console.WriteLine("1. SqlSugar SQLite 示例 ★推荐");
            Console.WriteLine("2. SqlSugar SQL Server 示例 ★推荐");
            Console.WriteLine("3. SqlSugar MySQL 示例 ★推荐");
            Console.WriteLine();
            Console.WriteLine("=== 传统方式 (需要写SQL) ===");
            Console.WriteLine("4. SQLite 示例 (旧版)");
            Console.WriteLine("5. SQL Server 示例 (旧版)");
            Console.WriteLine("6. MySQL 示例 (旧版)");
            Console.WriteLine();
            Console.WriteLine("=== 其他 ===");
            Console.WriteLine("7. 依赖注入示例");
            Console.WriteLine("8. 数据库工厂示例");
            Console.WriteLine("0. 返回主菜单");
            Console.WriteLine();
            Console.Write("请输入选项 (0-8): ");

            var input = Console.ReadLine();

            try
            {
                switch (input)
                {
                    case "1":
                        await SqliteSugarHelperExample.RunAllExamples();
                        break;

                    case "2":
                        await SqlServerSugarHelperExample.RunAllExamples();
                        break;

                    case "3":
                        await MySqlSugarHelperExample.RunAllExamples();
                        break;

                    case "4":
                        await SqliteHelperExample.RunAllExamples();
                        break;

                    case "5":
                        await SqlServerHelperExample.RunAllExamples();
                        break;

                    case "6":
                        await MySqlHelperExample.RunAllExamples();
                        break;

                    case "7":
                        await DependencyInjectionExample();
                        break;

                    case "8":
                        await DatabaseFactoryExample();
                        break;

                    case "0":
                        return;

                    default:
                        Console.WriteLine("无效的选项，请重新输入");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n运行示例时发生错误: {ex.Message}");
            }

            Console.WriteLine("\n按任意键继续...");
            Console.ReadKey();
            Console.Clear();
        }
    }

    /// <summary>
    /// 依赖注入示例
    /// </summary>
    private static async Task DependencyInjectionExample()
    {
        Console.WriteLine("\n=== 依赖注入示例 ===\n");

        // 配置服务
        var services = new ServiceCollection();

        // 添加 SQLite
        services.AddSqlite(options =>
        {
            options.DatabasePath = "di_example.db";
            options.JournalMode = SqliteJournalMode.Wal;
        });

        // 构建服务提供程序
        var serviceProvider = services.BuildServiceProvider();

        // 获取服务
        using var scope = serviceProvider.CreateScope();
        var dbHelper = scope.ServiceProvider.GetRequiredService<IDbHelper>();

        Console.WriteLine($"数据库类型: {dbHelper.DatabaseType}");
        Console.WriteLine($"连接字符串: {dbHelper.ConnectionString}");

        // 测试连接
        var connected = await dbHelper.TestConnectionAsync();
        Console.WriteLine($"连接测试: {(connected ? "成功" : "失败")}");

        if (connected)
        {
            // 简单操作
            await dbHelper.ExecuteNonQueryAsync(@"
                CREATE TABLE IF NOT EXISTS DITest (Id INTEGER PRIMARY KEY, Name TEXT)");

            await dbHelper.ExecuteNonQueryAsync(
                "INSERT INTO DITest (Name) VALUES (@Name)",
                new { Name = "DI测试数据" });

            var count = await dbHelper.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM DITest");
            Console.WriteLine($"记录数: {count}");

                    await dbHelper.ExecuteNonQueryAsync("DROP TABLE DITest");
                }

                // 等待连接完全关闭
                await Task.Delay(100);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

                // 清理
                try
                {
                    if (File.Exists("di_example.db"))
                    {
                        File.Delete("di_example.db");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? 清理数据库文件失败: {ex.Message}");
                }

                Console.WriteLine("\n? 依赖注入示例完成");
            }

    /// <summary>
    /// 数据库工厂示例
    /// </summary>
    private static async Task DatabaseFactoryExample()
    {
        Console.WriteLine("\n=== 数据库工厂示例 ===\n");

        // 配置服务
        var services = new ServiceCollection();
        services.AddDatabaseFactory();

        var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbHelperFactory>();

        Console.WriteLine("使用工厂创建不同类型的数据库帮助类:\n");

        // 创建 SQLite
        Console.WriteLine("1. 创建 SQLite 帮助类");
        using (var sqliteHelper = factory.CreateSqlite("factory_test.db"))
        {
            var connected = await sqliteHelper.TestConnectionAsync();
            Console.WriteLine($"   SQLite 连接: {(connected ? "成功" : "失败")}");

            if (connected)
            {
                await sqliteHelper.ExecuteNonQueryAsync(
                    "CREATE TABLE IF NOT EXISTS Test (Id INTEGER PRIMARY KEY)");
                await sqliteHelper.ExecuteNonQueryAsync("DROP TABLE Test");
            }
        }

        // 演示通过枚举创建
        Console.WriteLine("\n2. 通过 DatabaseType 枚举创建");
        using (var helper = factory.Create(DatabaseType.Sqlite, "Data Source=enum_test.db"))
        {
            Console.WriteLine($"   创建的类型: {helper.DatabaseType}");
            var connected = await helper.TestConnectionAsync();
            Console.WriteLine($"   连接测试: {(connected ? "成功" : "失败")}");
            }

            // 等待连接完全关闭
            await Task.Delay(100);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            // 清理
            foreach (var file in new[] { "factory_test.db", "enum_test.db" })
            {
                try
                {
                    if (File.Exists(file)) File.Delete(file);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? 清理文件 {file} 失败: {ex.Message}");
                }
            }

            Console.WriteLine("\n? 数据库工厂示例完成");
        }

    /// <summary>
    /// 快速演示（只运行 SQLite 示例）
    /// </summary>
    public static async Task QuickDemoAsync()
    {
        Console.WriteLine("=== ToolHelper.Database 快速演示 ===\n");
        await SqliteHelperExample.RunAllExamples();
    }
}
