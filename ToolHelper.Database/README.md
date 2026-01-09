# ToolHelper.Database

数据库操作工具类库，提供 SQLite、SQL Server、MySQL 数据库的统一操作封装。

## ?? 新增 SqlSugar ORM 支持 (推荐)

现在支持使用 **SqlSugar ORM**，无需手写 SQL 语句，使用 Lambda 表达式进行增删改查！

```csharp
using ToolHelper.Database.Sqlite;
using ToolHelper.Database.Configuration;

// 创建帮助类
var options = new SqliteSugarOptions { DatabasePath = "mydata.db" };
using var db = new SqliteSugarHelper(options);

// 自动创建表（Code First）
db.CreateTable<User>();

// 插入数据（无需写SQL）
var user = new User { Name = "张三", Age = 25 };
db.Insert(user);

// 查询数据（Lambda表达式）
var users = db.GetList<User>(u => u.Age > 18 && u.IsActive);

// 更新数据
db.Update<User>(u => new User { Age = 26 }, u => u.Name == "张三");

// 删除数据
db.Delete<User>(u => u.Age < 18);

// 分页查询
var result = await db.GetPageListAsync<User>(
    u => u.IsActive,
    pageIndex: 1,
    pageSize: 10,
    orderByExpression: u => u.CreateTime,
    isAsc: false);
```

## 功能特性

### SqlSugar ORM (推荐)
- ? **无需SQL** - 使用 Lambda 表达式进行查询
- ? **Code First** - 自动创建/更新表结构
- ? **高性能批量** - BulkCopy 高速批量操作
- ? **链式查询** - 流畅的链式 API
- ? **实体特性** - 使用特性配置表和列

### 传统方式
- ? **统一接口** - 所有数据库操作使用统一的 `IDbHelper` 接口
- ? **异步优先** - 所有 IO 操作使用 async/await
- ? **依赖注入** - 完整支持 Microsoft.Extensions.DependencyInjection
- ? **实体映射** - 自动将查询结果映射到实体类
- ? **事务支持** - 支持事务管理和自动回滚
- ? **批量操作** - 高性能批量插入、更新、删除
- ? **类型安全** - 参数化查询，防止 SQL 注入
- ? **流式查询** - 支持大数据量的流式处理

## 支持的数据库

| 数据库 | SqlSugar类 (推荐) | 传统类 | 特有功能 |
|--------|-------------------|--------|----------|
| SQLite | `SqliteSugarHelper` | `SqliteHelper` | 备份/恢复、VACUUM、完整性检查 |
| SQL Server | `SqlServerSugarHelper` | `SqlServerHelper` | 存储过程、BulkCopy、MERGE |
| MySQL | `MySqlSugarHelper` | `MySqlHelper` | BulkCopy、InsertOrUpdate |

## 快速开始

### 方式一：SqlSugar ORM (推荐)

```csharp
using SqlSugar;
using ToolHelper.Database.Configuration;
using ToolHelper.Database.Sqlite;

// 定义实体
[SugarTable("Users")]
public class User
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(Length = 50)]
    public string Name { get; set; }

    public int Age { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreateTime { get; set; }
}

// 使用
var options = new SqliteSugarOptions
{
    DatabasePath = "mydata.db",
    EnableSqlLog = true  // 启用SQL日志
};

using var db = new SqliteSugarHelper(options);

// 自动创建表
db.CreateTable<User>();

// CRUD 操作
db.Insert(user);                                    // 插入
var users = db.GetList<User>(u => u.IsActive);     // 查询
db.Update(user);                                    // 更新
db.Delete<User>(u => u.Id == 1);                   // 删除

// 高级查询
var result = db.Queryable<User>()
    .Where(u => u.Age > 18)
    .OrderBy(u => u.CreateTime, OrderByType.Desc)
    .Take(10)
    .ToList();
```

### 方式二：传统方式 (需要写SQL)

```csharp
using ToolHelper.Database.Sqlite;
using Microsoft.Extensions.Options;

// 创建 SQLite 帮助类
var options = Options.Create(new SqliteOptions
{
    DatabasePath = "mydata.db",
    JournalMode = SqliteJournalMode.Wal
});

using var helper = new SqliteHelper(options);

// 创建表
await helper.ExecuteNonQueryAsync(@"
    CREATE TABLE IF NOT EXISTS Users (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT NOT NULL,
        Email TEXT UNIQUE
    )");

// 插入数据
var id = await helper.InsertAndGetIdAsync(
    "INSERT INTO Users (Name, Email) VALUES (@Name, @Email)",
    new { Name = "张三", Email = "zhang@example.com" });

// 查询数据
var users = await helper.QueryAsync<User>("SELECT * FROM Users");

// 流式查询（大数据量）
await foreach (var user in helper.QueryStreamAsync<User>("SELECT * FROM Users"))
{
    Console.WriteLine(user.Name);
}
```

### 依赖注入

```csharp
// Program.cs - SqlSugar方式 (推荐)
services.AddSqliteSugar(options =>
{
    options.DatabasePath = "app.db";
    options.EnableSqlLog = true;
});

// 或 SQL Server
services.AddSqlServerSugar(options =>
{
    options.Server = "localhost";
    options.Database = "MyDb";
    options.UserId = "sa";
    options.Password = "password";
});

// 或 MySQL
services.AddMySqlSugar(options =>
{
    options.Server = "localhost";
    options.Database = "mydb";
    options.UserId = "root";
    options.Password = "password";
});

// 在类中注入
public class MyService
{
    private readonly ISqlSugarDbHelper _db;

    public MyService(ISqlSugarDbHelper db)
    {
        _db = db;
    }

    public async Task<List<User>> GetActiveUsersAsync()
    {
        return await _db.GetListAsync<User>(u => u.IsActive);
    }
}
```

### 传统依赖注入

```csharp
// Program.cs 或 Startup.cs
services.AddSqlite(options =>
{
    options.DatabasePath = "app.db";
    options.JournalMode = SqliteJournalMode.Wal;
});

// 或使用连接字符串
services.AddSqlServer("Server=localhost;Database=MyDb;User Id=sa;Password=xxx;");

// 在类中注入
public class MyService
{
    private readonly IDbHelper _dbHelper;

    public MyService(IDbHelper dbHelper)
    {
        _dbHelper = dbHelper;
    }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        return await _dbHelper.QueryAsync<User>("SELECT * FROM Users");
    }
}
```

### 数据库工厂

```csharp
// SqlSugar 工厂 (推荐)
services.AddSqlSugarFactory();

var factory = serviceProvider.GetRequiredService<ISqlSugarDbHelperFactory>();
var sqliteHelper = factory.CreateSqlite("mydata.db");
var sqlServerHelper = factory.CreateSqlServer(new SqlServerSugarOptions { ... });
var mysqlHelper = factory.CreateMySql("connection string");

// 传统工厂
services.AddDatabaseFactory();

var factory = serviceProvider.GetRequiredService<IDbHelperFactory>();
var helper = factory.Create(DatabaseType.Sqlite, "Data Source=test.db");
```

## 详细用法

### SqlSugar 批量操作

```csharp
// 批量插入（高性能）
var users = new List<User> { ... };
db.InsertRange(users);           // 普通批量
db.BulkInsert(users);            // SQL Server/MySQL 高速批量 (BulkCopy)

// 批量更新
db.UpdateRange(users);
db.BulkUpdate(users);            // 高速批量更新

// 批量删除
db.DeleteByIds<User>(new object[] { 1, 2, 3 });

// 批量合并（Insert or Update）
db.BulkMerge(users);             // SQL Server/MySQL
db.InsertOrUpdate(user);         // MySQL
```

### SqlSugar 事务操作

```csharp
// 方式一：自动事务
db.ExecuteInTransaction(() =>
{
    db.Insert(user);
    db.Update(order);
    db.Delete<Log>(l => l.CreateTime < DateTime.Now.AddDays(-30));
});

// 方式二：异步事务
await db.ExecuteInTransactionAsync(async () =>
{
    await db.InsertAsync(user);
    await db.UpdateAsync(order);
});

// 方式三：手动控制
db.BeginTran();
try
{
    db.Insert(user);
    db.Update(order);
    db.CommitTran();
}
catch
{
    db.RollbackTran();
    throw;
}
```

### SqlSugar 复杂查询

```csharp
// 链式查询
var result = db.Queryable<User>()
    .Where(u => u.Age > 18)
    .Where(u => u.IsActive)
    .WhereIF(!string.IsNullOrEmpty(keyword), u => u.Name.Contains(keyword))
    .OrderBy(u => u.CreateTime, OrderByType.Desc)
    .Select(u => new { u.Name, u.Age })
    .ToPageList(pageIndex, pageSize);

// 聚合查询
var avgAge = db.Queryable<User>().Avg(u => u.Age);
var maxAge = db.Queryable<User>().Max(u => u.Age);
var count = db.Queryable<User>().Count();

// 分组查询
var groups = db.Queryable<User>()
    .GroupBy(u => u.Age)
    .Select(u => new { Age = u.Age, Count = SqlFunc.AggregateCount(u.Id) })
    .ToList();
```

### 传统事务操作

```csharp
// 方式1: 使用 ExecuteInTransactionAsync
await helper.ExecuteInTransactionAsync(async transaction =>
{
    await helper.ExecuteNonQueryAsync("INSERT INTO ...", parameters);
    await helper.ExecuteNonQueryAsync("UPDATE ...", parameters);
    // 自动提交，异常时自动回滚
});

// 方式2: 手动管理事务
using var transaction = await helper.BeginTransactionAsync();
try
{
    await helper.ExecuteNonQueryAsync("INSERT INTO ...", parameters);
    await helper.ExecuteNonQueryAsync("UPDATE ...", parameters);
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### 批量操作

```csharp
// 批量插入
var products = new List<Product>
{
    new Product { Name = "产品A", Price = 100 },
    new Product { Name = "产品B", Price = 200 },
    // ... 更多数据
};

var insertedCount = await helper.BulkInsertAsync("Products", products);

// SQL Server 特有: MERGE 操作（插入或更新）
await sqlServerHelper.BulkMergeAsync("Products", products, new[] { "Id" });

// MySQL 特有: INSERT ON DUPLICATE KEY UPDATE
await mysqlHelper.InsertOrUpdateAsync("Products", product, new[] { "Name", "Price" });
```

### 存储过程

```csharp
// SQL Server
var result = await sqlServerHelper.ExecuteStoredProcedureAsync<User>(
    "sp_GetUserById",
    new { UserId = 1 });

// 带输出参数
var outputs = await sqlServerHelper.ExecuteStoredProcedureWithOutputAsync(
    "sp_GetStats",
    new { Category = "电子" },
    new Dictionary<string, SqlDbType>
    {
        ["TotalCount"] = SqlDbType.Int,
        ["AvgPrice"] = SqlDbType.Decimal
    });

Console.WriteLine($"总数: {outputs["TotalCount"]}");
```

### 数据库管理

```csharp
// SQLite
await sqliteHelper.BackupToFileAsync("backup.db");   // 备份
await sqliteHelper.VacuumAsync();                    // 整理
var results = await sqliteHelper.IntegrityCheckAsync(); // 完整性检查

// SQL Server
var dbInfo = await sqlServerHelper.GetDatabaseInfoAsync();
await sqlServerHelper.TruncateTableAsync("TableName");

// MySQL
await mysqlHelper.AnalyzeTableAsync("TableName");    // 分析表
await mysqlHelper.OptimizeTableAsync("TableName");   // 优化表
await mysqlHelper.CheckTableAsync("TableName");      // 检查表
```

## 配置选项

### SqliteOptions

```csharp
new SqliteOptions
{
    DatabasePath = "data.db",      // 数据库文件路径
    InMemory = false,              // 是否使用内存数据库
    JournalMode = SqliteJournalMode.Wal,  // WAL模式（推荐）
    SynchronousMode = SqliteSynchronousMode.Normal,
    CacheSize = 2000,              // 缓存大小（页）
    ForeignKeys = true,            // 外键约束
    Password = null,               // 加密密码
    CommandTimeout = 30,           // 命令超时（秒）
    BatchSize = 1000               // 批量操作大小
}
```

### SqlServerOptions

```csharp
new SqlServerOptions
{
    Server = "localhost",
    Port = 1433,
    Database = "MyDb",
    UserId = "sa",
    Password = "password",
    IntegratedSecurity = false,    // Windows认证
    Encrypt = true,
    TrustServerCertificate = true,
    MultipleActiveResultSets = true,
    CommandTimeout = 30,
    RetryCount = 3,                // 瞬态错误重试次数
    RetryIntervalMs = 1000
}
```

### MySqlOptions

```csharp
new MySqlOptions
{
    Server = "localhost",
    Port = 3306,
    Database = "mydb",
    UserId = "root",
    Password = "password",
    Charset = "utf8mb4",
    SslMode = MySqlSslMode.Preferred,
    AllowUserVariables = true,
    CommandTimeout = 30,
    BatchSize = 1000
}
```

## 实体映射

实体类属性会自动与查询结果列进行匹配（不区分大小写）：

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

// 自动映射
var users = await helper.QueryAsync<User>("SELECT Id, Name, Email, CreatedAt, IsActive FROM Users");
```

## 项目结构

```
ToolHelper.Database/
├── Abstractions/                  # 接口定义
│   ├── IDbHelper.cs              # 数据库帮助类接口
│   ├── IDataStorage.cs           # 数据存储接口
│   └── IDbConnectionFactory.cs   # 连接工厂接口
├── Configuration/                 # 配置类
│   └── DatabaseOptions.cs        # 数据库配置选项
├── Core/                          # 核心实现
│   └── DbHelperBase.cs           # 基类实现
├── Sqlite/                        # SQLite 实现
│   └── SqliteHelper.cs
├── SqlServer/                     # SQL Server 实现
│   └── SqlServerHelper.cs
├── MySql/                         # MySQL 实现
│   └── MySqlHelper.cs
├── Extensions/                    # 扩展方法
│   └── ServiceCollectionExtensions.cs
└── README.md
```

## 性能优化

1. **连接池** - 默认启用连接池，复用数据库连接
2. **类型缓存** - 缓存实体类型的属性信息，减少反射开销
3. **批量操作** - 使用数据库原生批量导入功能
4. **流式查询** - 大数据量使用 `QueryStreamAsync` 避免内存溢出
5. **预编译语句** - SQLite 批量插入使用预编译语句

## 注意事项

1. **资源释放** - `IDbHelper` 实现了 `IDisposable` 和 `IAsyncDisposable`，使用完毕后请释放
2. **SQL注入** - 始终使用参数化查询，避免字符串拼接
3. **事务** - 长事务会锁定资源，尽量保持事务简短
4. **连接** - 高并发场景注意调整连接池大小

## 版本历史

### v1.0.0
- 初始版本
- 支持 SQLite、SQL Server、MySQL
- 完整的 CRUD 操作
- 事务支持
- 批量操作
- 依赖注入支持

## 许可证

MIT License
