using SqlSugar;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.SqlServer;

namespace ToolHelperTest.Examples.Database;

/// <summary>
/// SqlSugar SQL Server 帮助类使用示例
/// </summary>
public class SqlServerSugarHelperExample
{
    /// <summary>
    /// 运行所有示例（需要真实的SQL Server连接）
    /// </summary>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("=== SqlSugar SQL Server 帮助类示例 ===");
        Console.WriteLine("注意：需要配置真实的 SQL Server 连接\n");

        // 配置选项（请修改为实际的连接信息）
        var options = new SqlServerSugarOptions
        {
            Server = "localhost",
            Database = "TestDB",
            UserId = "sa",
            Password = "YourPassword",
            TrustServerCertificate = true,
            EnableSqlLog = true,
            OnLogExecuting = (sql, pars) =>
            {
                Console.WriteLine($"[SQL] {sql}");
            }
        };

        // 由于需要真实连接，这里只展示代码示例
        ShowCodeExamples();

        Console.WriteLine("\n=== SQL Server 示例结束 ===");
    }

    /// <summary>
    /// 代码示例展示
    /// </summary>
    static void ShowCodeExamples()
    {
        Console.WriteLine("--- 代码示例（展示如何使用） ---\n");

        Console.WriteLine(@"
// 1. 创建帮助类
var options = new SqlServerSugarOptions
{
    Server = ""localhost"",
    Database = ""MyDatabase"",
    UserId = ""sa"",
    Password = ""YourPassword"",
    EnableSqlLog = true
};

using var db = new SqlServerSugarHelper(options);

// 2. 自动创建表（Code First）
db.CreateTable<Product>();

// 3. 插入数据（无需写SQL）
var product = new Product { Name = ""商品A"", Price = 99.9m };
db.Insert(product);

// 4. 查询数据（Lambda表达式）
var products = db.GetList<Product>(p => p.Price > 50);

// 5. 更新数据
db.Update<Product>(
    p => new Product { Price = 199.9m },
    p => p.Name == ""商品A"");

// 6. 高性能批量插入（使用 BulkCopy）
var productList = new List<Product> { ... };
db.BulkInsert(productList);

// 7. 批量更新
db.BulkUpdate(productList);

// 8. 批量合并（Insert or Update）
db.BulkMerge(productList);

// 9. 执行存储过程
var result = db.ExecuteStoredProcedure<Product>(""sp_GetProducts"", new { CategoryId = 1 });

// 10. 事务操作
db.ExecuteInTransaction(() => {
    db.Insert(product1);
    db.Update(product2);
    db.Delete<Product>(p => p.Id == 3);
});

// 11. 分页查询
var pagedResult = await db.GetPageListAsync<Product>(
    whereExpression: p => p.IsActive,
    pageIndex: 1,
    pageSize: 20,
    orderByExpression: p => p.CreateTime,
    isAsc: false);

// 12. 复杂查询（链式调用）
var query = db.Queryable<Product>()
    .Where(p => p.Price > 100)
    .Where(p => p.IsActive)
    .OrderBy(p => p.Name)
    .Select(p => new { p.Name, p.Price })
    .ToPageList(1, 10);
");

        Console.WriteLine("SQL Server 特有功能：");
        Console.WriteLine(@"
// 获取数据库信息
var dbInfo = db.GetDatabaseInfo();
Console.WriteLine($""数据大小: {dbInfo.DataSizeMB}MB"");
Console.WriteLine($""日志大小: {dbInfo.LogSizeMB}MB"");

// 获取所有表名
var tables = db.GetAllTableNames();

// 收缩数据库
db.ShrinkDatabase();

// 截断表
db.TruncateTable(""Products"");
");
    }

        /// <summary>
        /// 实际测试（需要连接真实数据库时取消注释）
        /// </summary>
        public static async Task RunActualTest()
        {
            var options = new SqlServerSugarOptions
            {
                Server = "localhost",
                Database = "TestDB",
                UserId = "sa",
                Password = "YourPassword",
                TrustServerCertificate = true,
                EnableSqlLog = true
            };

            using (var db = new SqlServerSugarHelper(options))
            {
                // 测试连接
                if (!db.TestConnection())
                {
                    Console.WriteLine("连接失败！");
                    return;
                }

                Console.WriteLine("连接成功！");

                // 创建表
                db.CreateTable<SugarProduct>();

                // 插入测试数据
                var products = Enumerable.Range(1, 1000)
                    .Select(i => new SugarProduct
                    {
                        Name = $"商品{i}",
                        Price = i * 10.5m,
                        Stock = i * 10,
                        IsActive = i % 2 == 0,
                        CreateTime = DateTime.Now
                    })
                    .ToList();

                // 高性能批量插入
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var count = db.BulkInsert(products);
                sw.Stop();
                Console.WriteLine($"批量插入 {count} 条记录，耗时: {sw.ElapsedMilliseconds}ms");

                // 查询测试
                var activeProducts = await db.GetListAsync<SugarProduct>(p => p.IsActive && p.Price > 100);
                Console.WriteLine($"查询到 {activeProducts.Count} 条活跃且价格>100的商品");

                // 分页查询
                var pagedResult = await db.GetPageListAsync<SugarProduct>(
                    p => p.IsActive,
                    1, 10,
                    p => p.Price, false);
                Console.WriteLine($"分页结果: {pagedResult.TotalCount} 条总记录, {pagedResult.TotalPages} 页");
            }

            Console.WriteLine("\n=== 示例完成 ===");
        }
    }

    /// <summary>
        /// 产品实体 - SqlSugar SQL Server 示例专用
        /// </summary>
        [SugarTable("SugarProducts")]
        public class SugarProduct
        {
            [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
            public int Id { get; set; }

            [SugarColumn(Length = 100)]
            public string Name { get; set; } = string.Empty;

            [SugarColumn(DecimalDigits = 2)]
            public decimal Price { get; set; }

            public int Stock { get; set; }

            public bool IsActive { get; set; }

            public DateTime CreateTime { get; set; }
        }

