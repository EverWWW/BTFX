using SqlSugar;
using ToolHelper.Database.Abstractions;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Sqlite;

namespace ToolHelperTest.Examples.Database;

/// <summary>
/// SqlSugar SQLite 帮助类使用示例
/// 展示基于 ORM 的数据库操作，无需编写 SQL 语句
/// </summary>
public class SqliteSugarHelperExample
{
    /// <summary>
    /// 运行所有示例
    /// </summary>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("=== SqlSugar SQLite 帮助类示例（推荐使用） ===");
        Console.WriteLine("基于 SqlSugar ORM，无需编写 SQL 语句\n");

        // 创建数据库帮助类
        var options = new SqliteSugarOptions
        {
            DatabasePath = "sqlsugar_demo.db",
            EnableSqlLog = true,
            OnLogExecuting = (sql, pars) =>
            {
                Console.WriteLine($"[SQL] {sql}");
            }
        };

        using (var db = new SqliteSugarHelper(options))
        {
            // 自动创建表
            db.CreateTable<SugarUser>();
            db.CreateTable<SugarOrder>();

            Console.WriteLine("? 表创建成功\n");

            // 1. 插入数据
            await InsertExample(db);

            // 2. 查询数据
            await QueryExample(db);

            // 3. 更新数据
            await UpdateExample(db);

            // 4. 删除数据
            await DeleteExample(db);

            // 5. 事务示例
            await TransactionExample(db);

            // 6. 高级查询
            await AdvancedQueryExample(db);

            // 7. SQLite 特有功能
            SqliteSpecificFeatures(db);
        } // 确保 using 块结束，资源被释放

        // 等待连接完全关闭
        await Task.Delay(100);
        GC.Collect();
        GC.WaitForPendingFinalizers();

        // 清理数据库文件
        try
        {
            if (File.Exists("sqlsugar_demo.db"))
            {
                File.Delete("sqlsugar_demo.db");
                Console.WriteLine("\n? 数据库文件已清理");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n? 清理数据库文件失败: {ex.Message}");
            Console.WriteLine("请手动删除 sqlsugar_demo.db 文件");
        }

        Console.WriteLine("\n=== 示例完成 ===");
    }

    /// <summary>
    /// 插入数据示例
    /// </summary>
    static async Task InsertExample(SqliteSugarHelper db)
    {
        Console.WriteLine("--- 1. 插入数据 ---");

        // 单条插入
        var user = new SugarUser
        {
            Name = "张三",
            Email = "zhangsan@example.com",
            Age = 25,
            IsActive = true,
            CreateTime = DateTime.Now
        };

        // 插入并返回自增ID
        var id = db.InsertReturnIdentity(user);
        Console.WriteLine($"插入用户成功，ID: {id}");

        // 批量插入
        var users = new List<SugarUser>
        {
            new() { Name = "李四", Email = "lisi@example.com", Age = 30, IsActive = true, CreateTime = DateTime.Now },
            new() { Name = "王五", Email = "wangwu@example.com", Age = 28, IsActive = false, CreateTime = DateTime.Now },
            new() { Name = "赵六", Email = "zhaoliu@example.com", Age = 35, IsActive = true, CreateTime = DateTime.Now }
        };

        var count = await db.InsertRangeAsync(users);
        Console.WriteLine($"批量插入成功，共 {count} 条记录\n");
    }

    /// <summary>
    /// 查询数据示例
    /// </summary>
    static async Task QueryExample(SqliteSugarHelper db)
    {
        Console.WriteLine("--- 2. 查询数据 ---");

        // 根据ID查询
        var user = db.GetById<SugarUser>(1);
        Console.WriteLine($"查询ID=1: {user?.Name}");

        // 条件查询（Lambda表达式，无需写SQL）
        var activeUsers = await db.GetListAsync<SugarUser>(u => u.IsActive);
        Console.WriteLine($"查询活跃用户: {activeUsers.Count} 条");

        // 复杂条件查询
        var filteredUsers = await db.GetListAsync<SugarUser>(u =>
            u.Age >= 25 && u.Age <= 35 && u.IsActive);
        Console.WriteLine($"查询年龄25-35的活跃用户: {filteredUsers.Count} 条");

        // 获取单条记录
        var firstUser = db.GetFirst<SugarUser>(u => u.Email != null && u.Email.Contains("@example.com"));
        Console.WriteLine($"第一条邮箱包含@example.com的用户: {firstUser?.Name}");

        // 判断是否存在
        var exists = await db.AnyAsync<SugarUser>(u => u.Name == "张三");
        Console.WriteLine($"是否存在张三: {exists}");

        // 统计数量
        var userCount = await db.CountAsync<SugarUser>(u => u.IsActive);
        Console.WriteLine($"活跃用户数量: {userCount}");

        // 分页查询
        var pagedResult = await db.GetPageListAsync<SugarUser>(
            whereExpression: u => u.IsActive,
            pageIndex: 1,
            pageSize: 10,
            orderByExpression: u => u.CreateTime,
            isAsc: false);
        Console.WriteLine($"分页查询: 第{pagedResult.PageIndex}页，共{pagedResult.TotalCount}条，{pagedResult.TotalPages}页\n");
    }

    /// <summary>
    /// 更新数据示例
    /// </summary>
    static async Task UpdateExample(SqliteSugarHelper db)
    {
        Console.WriteLine("--- 3. 更新数据 ---");

        // 根据实体更新
        var user = db.GetById<SugarUser>(1);
        if (user != null)
        {
            user.Age = 26;
            user.Email = "zhangsan_updated@example.com";
            var success = await db.UpdateAsync(user);
            Console.WriteLine($"更新用户: {(success ? "成功" : "失败")}");
        }

        // 条件更新（只更新指定字段，无需加载实体）
        var updatedCount = await db.UpdateAsync<SugarUser>(
            columns: u => new SugarUser { IsActive = false },
            whereExpression: u => u.Age > 30);
        Console.WriteLine($"批量更新年龄>30的用户状态: {updatedCount} 条\n");
    }

    /// <summary>
    /// 删除数据示例
    /// </summary>
    static async Task DeleteExample(SqliteSugarHelper db)
    {
        Console.WriteLine("--- 4. 删除数据 ---");

        // 根据条件删除
        var deletedCount = await db.DeleteAsync<SugarUser>(u => u.Age > 34);
        Console.WriteLine($"删除年龄>34的用户: {deletedCount} 条\n");
    }

    /// <summary>
    /// 事务示例
    /// </summary>
    static async Task TransactionExample(SqliteSugarHelper db)
    {
        Console.WriteLine("--- 5. 事务操作 ---");

        try
        {
            await db.ExecuteInTransactionAsync(async () =>
            {
                // 在事务中执行多个操作
                var order = new SugarOrder
                {
                    UserId = 1,
                    Amount = 99.99m,
                    OrderTime = DateTime.Now,
                    Status = "Pending"
                };
                db.Insert(order);

                // 更新用户
                await db.UpdateAsync<SugarUser>(
                    u => new SugarUser { Age = 27 },
                    u => u.Id == 1);

                Console.WriteLine("事务执行成功");
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"事务执行失败: {ex.Message}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// 高级查询示例
    /// </summary>
    static async Task AdvancedQueryExample(SqliteSugarHelper db)
    {
        Console.WriteLine("--- 6. 高级查询（链式调用） ---");

        // 使用 Queryable 进行链式查询
        var result = await db.Queryable<SugarUser>()
            .Where(u => u.IsActive)
            .OrderBy(u => u.Age)
            .Select(u => new { u.Name, u.Age, u.Email })
            .ToListAsync();

        Console.WriteLine($"链式查询结果: {result.Count} 条");

        // 模糊查询
        var likeResult = db.Queryable<SugarUser>()
            .Where(u => u.Name.Contains("三") || (u.Email != null && u.Email.StartsWith("li")))
            .ToList();
        Console.WriteLine($"模糊查询结果: {likeResult.Count} 条");

        // 聚合查询
        var avgAge = db.Queryable<SugarUser>().Avg(u => u.Age);
        var maxAge = db.Queryable<SugarUser>().Max(u => u.Age);
        var minAge = db.Queryable<SugarUser>().Min(u => u.Age);
        Console.WriteLine($"年龄统计 - 平均: {avgAge}, 最大: {maxAge}, 最小: {minAge}\n");
    }

    /// <summary>
    /// SQLite 特有功能
    /// </summary>
    static void SqliteSpecificFeatures(SqliteSugarHelper db)
    {
        Console.WriteLine("--- 7. SQLite 特有功能 ---");

        // 获取数据库大小
        var size = db.GetDatabaseSize();
        Console.WriteLine($"数据库大小: {size} 字节");

        // 获取所有表名
        var tables = db.GetAllTableNames();
        Console.WriteLine($"数据库表: {string.Join(", ", tables)}");

        // 获取日志模式
        var journalMode = db.GetJournalMode();
        Console.WriteLine($"日志模式: {journalMode}");


                // 完整性检查
                var checkResult = db.IntegrityCheck();
                Console.WriteLine($"完整性检查: {string.Join(", ", checkResult)}");
            }
        }

        /// <summary>
        /// 用户实体（使用 SqlSugar 特性）- SqlSugar 示例专用
        /// </summary>
        [SugarTable("SugarUsers")]
        public class SugarUser
        {
            /// <summary>用户ID</summary>
            [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
            public int Id { get; set; }

            /// <summary>用户名</summary>
            [SugarColumn(Length = 50)]
            public string Name { get; set; } = string.Empty;

            /// <summary>邮箱</summary>
            [SugarColumn(Length = 100, IsNullable = true)]
            public string? Email { get; set; }

            /// <summary>年龄</summary>
            public int Age { get; set; }

            /// <summary>是否活跃</summary>
            public bool IsActive { get; set; }

            /// <summary>创建时间</summary>
            public DateTime CreateTime { get; set; }
        }

        /// <summary>
        /// 订单实体 - SqlSugar 示例专用
        /// </summary>
        [SugarTable("SugarOrders")]
        public class SugarOrder
        {
            /// <summary>订单ID</summary>
            [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
            public int Id { get; set; }

            /// <summary>用户ID</summary>
            public int UserId { get; set; }

            /// <summary>订单金额</summary>
            [SugarColumn(DecimalDigits = 2)]
            public decimal Amount { get; set; }

            /// <summary>订单时间</summary>
            public DateTime OrderTime { get; set; }

            /// <summary>订单状态</summary>
            [SugarColumn(Length = 20)]
            public string Status { get; set; } = string.Empty;
        }

