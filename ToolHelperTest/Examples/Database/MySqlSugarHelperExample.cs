using SqlSugar;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.MySql;

namespace ToolHelperTest.Examples.Database;

/// <summary>
/// SqlSugar MySQL 帮助类使用示例
/// </summary>
public class MySqlSugarHelperExample
{
    /// <summary>
    /// 运行所有示例（需要真实的MySQL连接）
    /// </summary>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("=== SqlSugar MySQL 帮助类示例 ===");
        Console.WriteLine("注意：需要配置真实的 MySQL 连接\n");

        // 配置选项（请修改为实际的连接信息）
        var options = new MySqlSugarOptions
        {
            Server = "localhost",
            Port = 3306,
            Database = "testdb",
            UserId = "root",
            Password = "password",
            EnableSqlLog = true,
            OnLogExecuting = (sql, pars) =>
            {
                Console.WriteLine($"[SQL] {sql}");
            }
        };

        // 由于需要真实连接，这里只展示代码示例
        ShowCodeExamples();

        Console.WriteLine("\n=== MySQL 示例结束 ===");
    }

    /// <summary>
    /// 代码示例展示
    /// </summary>
    static void ShowCodeExamples()
    {
        Console.WriteLine("--- 代码示例（展示如何使用） ---\n");

        Console.WriteLine(@"
// 1. 创建帮助类
var options = new MySqlSugarOptions
{
    Server = ""localhost"",
    Port = 3306,
    Database = ""mydb"",
    UserId = ""root"",
    Password = ""password"",
    EnableSqlLog = true
};

using var db = new MySqlSugarHelper(options);

// 2. 自动创建表（Code First）
db.CreateTable<Article>();

// 3. 插入数据（无需写SQL）
var article = new Article { Title = ""文章标题"", Content = ""内容..."" };
db.Insert(article);

// 4. 查询数据（Lambda表达式）
var articles = db.GetList<Article>(a => a.IsPublished);

// 5. 更新数据
db.Update<Article>(
    a => new Article { ViewCount = a.ViewCount + 1 },
    a => a.Id == 1);

// 6. 高性能批量插入
var articleList = new List<Article> { ... };
db.BulkInsert(articleList);

// 7. 插入或更新（ON DUPLICATE KEY UPDATE）
db.InsertOrUpdate(article);

// 8. 批量插入或更新
db.InsertOrUpdateRange(articleList);

// 9. 批量合并
db.BulkMerge(articleList);

// 10. 执行存储过程
var result = db.ExecuteStoredProcedure<Article>(""sp_GetArticles"");

// 11. 事务操作
await db.ExecuteInTransactionAsync(async () => {
    db.Insert(article1);
    await db.UpdateAsync(article2);
});

// 12. 分页查询
var pagedResult = await db.GetPageListAsync<Article>(
    whereExpression: a => a.IsPublished,
    pageIndex: 1,
    pageSize: 20,
    orderByExpression: a => a.CreateTime,
    isAsc: false);

// 13. 复杂查询（链式调用）
var query = db.Queryable<Article>()
    .Where(a => a.Title.Contains(""关键字""))
    .Where(a => a.IsPublished)
    .OrderBy(a => a.ViewCount, OrderByType.Desc)
    .Take(10)
    .ToList();
");

        Console.WriteLine("MySQL 特有功能：");
        Console.WriteLine(@"
// 获取数据库信息
var dbInfo = db.GetDatabaseInfo();
Console.WriteLine($""数据大小: {dbInfo.DataSizeMB}MB"");
Console.WriteLine($""索引大小: {dbInfo.IndexSizeMB}MB"");
Console.WriteLine($""表数量: {dbInfo.TableCount}"");

// 获取服务器版本
var version = db.GetServerVersion();
Console.WriteLine($""MySQL版本: {version}"");

// 获取当前连接数
var connectionCount = db.GetConnectionCount();

// 获取所有表名
var tables = db.GetAllTableNames();

// 优化表
db.OptimizeTable(""Articles"");

// 分析表
db.AnalyzeTable(""Articles"");

// 检查表
var checkResult = db.CheckTable(""Articles"");

// 截断表
db.TruncateTable(""Articles"");
");
    }

    /// <summary>
    /// 实际测试（需要连接真实数据库时取消注释）
    /// </summary>
    public static async Task RunActualTest()
    {
        var options = new MySqlSugarOptions
        {
            Server = "localhost",
            Port = 3306,
            Database = "testdb",
            UserId = "root",
            Password = "password",
                    EnableSqlLog = true
                    };

                    using (var db = new MySqlSugarHelper(options))
                    {
                        // 测试连接
                        if (!db.TestConnection())
                        {
                            Console.WriteLine("连接失败！");
                            return;
                        }

                        Console.WriteLine("连接成功！");
                        Console.WriteLine($"MySQL版本: {db.GetServerVersion()}");

                        // 创建表
                        db.CreateTable<SugarArticle>();

                        // 插入测试数据
                        var articles = Enumerable.Range(1, 100)
                            .Select(i => new SugarArticle
                            {
                                Title = $"文章标题{i}",
                                Content = $"这是文章{i}的内容...",
                                AuthorId = i % 10 + 1,
                                ViewCount = i * 100,
                                IsPublished = i % 2 == 0,
                                CreateTime = DateTime.Now.AddDays(-i),
                                UpdateTime = DateTime.Now
                            })
                            .ToList();

                        // 批量插入或更新
                        var count = db.InsertOrUpdateRange(articles);
                        Console.WriteLine($"插入或更新 {count} 条记录");

                        // 查询测试
                        var publishedArticles = await db.GetListAsync<SugarArticle>(a => a.IsPublished);
                        Console.WriteLine($"查询到 {publishedArticles.Count} 篇已发布文章");

                        // 分页查询
                        var pagedResult = await db.GetPageListAsync<SugarArticle>(
                            a => a.IsPublished,
                            1, 10,
                            a => a.ViewCount, false);
                        Console.WriteLine($"分页结果: 总{pagedResult.TotalCount}条, {pagedResult.TotalPages}页");

                        // 聚合查询
                        var totalViews = db.Queryable<SugarArticle>().Sum(a => a.ViewCount);
                        var avgViews = db.Queryable<SugarArticle>().Avg(a => a.ViewCount);
                        Console.WriteLine($"总浏览量: {totalViews}, 平均浏览量: {avgViews}");
                    }

                    Console.WriteLine("\n=== 示例完成 ===");
                }
            }

            /// <summary>
/// 文章实体 - SqlSugar MySQL 示例专用
/// </summary>
[SugarTable("SugarArticles")]
public class SugarArticle
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 200)]
    public string Title { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? Content { get; set; }

    public int AuthorId { get; set; }

    public int ViewCount { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreateTime { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? UpdateTime { get; set; }
}

