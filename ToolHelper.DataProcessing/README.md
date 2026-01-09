# ToolHelper.DataProcessing 数据处理工具类库

## ?? 简介

`ToolHelper.DataProcessing` 是一个功能丰富的数据处理工具类库，为上位机软件开发提供统一、高效的文件处理能力。

## ? 特性

- **模块化设计**：每个Helper独立封装，按需引用
- **依赖注入支持**：完整的DI容器集成
- **异步优先**：所有IO操作使用async/await
- **高性能**：使用内存池、流式处理优化性能
- **类型安全**：完整的泛型支持和类型转换
- **线程安全**：并发场景下的安全操作
- **配置驱动**：灵活的配置选项

## ?? 已实现的工具类

| 工具类 | 功能描述 | 状态 |
|--------|----------|------|
| `CsvHelper<T>` | CSV文件读写，支持大文件流式处理 | ? 完成 |
| `JsonHelper` | JSON序列化、美化、压缩、路径查询 | ? 完成 |
| `XmlHelper` | XML序列化/反序列化、XPath查询 | ? 完成 |
| `IniFileHelper` | INI配置文件读写 | ? 完成 |
| `YamlHelper` | YAML配置文件处理 | ? 完成 |
| `ExcelHelper` | Excel操作（基于NPOI） | ? 完成 |
| `PdfHelper` | PDF生成与解析 | ? 完成 |

## ?? 快速开始

### 安装

```bash
# 在项目中添加引用
dotnet add reference path/to/ToolHelper.DataProcessing.csproj
```

### 基础使用

#### 1. CSV 文件处理

```csharp
using ToolHelper.DataProcessing.Csv;

// 创建Helper
var csvHelper = new CsvHelper<Person>();

// 写入CSV
var data = new List<Person>
{
    new() { Id = 1, Name = "张三", Age = 25 }
};
await csvHelper.WriteAsync("data.csv", data);

// 读取CSV
var result = await csvHelper.ReadAsync("data.csv");

// 流式读取大文件
await foreach (var person in csvHelper.ReadStreamAsync("large.csv"))
{
    Console.WriteLine(person.Name);
}
```

#### 2. JSON 处理

```csharp
using ToolHelper.DataProcessing.Json;

var jsonHelper = new JsonHelper();

// 序列化
var json = jsonHelper.Serialize(myObject);

// 反序列化
var obj = jsonHelper.Deserialize<MyType>(json);

// 美化JSON
var formatted = jsonHelper.Beautify(compactJson);

// 压缩JSON
var minified = jsonHelper.Minify(formattedJson);

// 文件操作
await jsonHelper.SerializeToFileAsync("config.json", myConfig);
var config = await jsonHelper.DeserializeFromFileAsync<Config>("config.json");
```

#### 3. XML 处理

```csharp
using ToolHelper.DataProcessing.Xml;

var xmlHelper = new XmlHelper();

// 序列化
var xml = xmlHelper.Serialize(myObject);

// 反序列化
var obj = xmlHelper.Deserialize<MyType>(xml);

// XPath查询
var value = xmlHelper.SelectSingleNode(xml, "//User/Name");
var values = xmlHelper.SelectNodes(xml, "//User");
```

#### 4. INI 文件处理

```csharp
using ToolHelper.DataProcessing.Ini;

var iniHelper = new IniFileHelper();

// 加载INI文件
await iniHelper.LoadAsync("config.ini");

// 读取配置
var host = iniHelper.Read("Database", "Host");
var port = iniHelper.Read<int>("Database", "Port");

// 写入配置
iniHelper.Write("Database", "Host", "localhost");
iniHelper.Write("Database", "Port", 3306);

// 保存文件
await iniHelper.SaveAsync("config.ini");
```

### 使用依赖注入

```csharp
using Microsoft.Extensions.DependencyInjection;
using ToolHelper.DataProcessing.Extensions;

var services = new ServiceCollection();

// 添加所有数据处理服务
services.AddDataProcessing();

// 或单独添加
services.AddCsvHelper(options =>
{
    options.Delimiter = ",";
    options.HasHeader = true;
});

services.AddJsonHelper(options =>
{
    options.Indented = true;
    options.PropertyNamingPolicy = "CamelCase";
});

var serviceProvider = services.BuildServiceProvider();

// 获取服务
var csvHelper = serviceProvider.GetRequiredService<CsvHelper<Person>>();
var jsonHelper = serviceProvider.GetRequiredService<JsonHelper>();
```

## ?? 配置选项

### CSV 配置

```csharp
services.AddCsvHelper(options =>
{
    options.Delimiter = ",";          // 分隔符
    options.HasHeader = true;         // 是否包含标题
    options.Quote = '"';              // 引用符
    options.Encoding = "UTF-8";       // 编码
    options.TrimFields = true;        // 去除空格
    options.StreamBatchSize = 1000;   // 流式批次大小
});
```

### JSON 配置

```csharp
services.AddJsonHelper(options =>
{
    options.Indented = true;                      // 格式化
    options.IgnoreNullValues = false;             // 忽略空值
    options.PropertyNamingPolicy = "CamelCase";   // 命名策略
    options.UseStringEnumConverter = true;        // 字符串枚举
});
```

### XML 配置

```csharp
services.AddXmlHelper(options =>
{
    options.RootElement = "Root";         // 根元素
    options.Indent = true;                // 缩进
    options.Encoding = "UTF-8";           // 编码
    options.OmitXmlDeclaration = false;   // 省略声明
});
```

### INI 配置

```csharp
services.AddIniHelper(options =>
{
    options.CommentChar = ';';       // 注释字符
    options.Separator = '=';         // 分隔符
    options.CaseSensitive = false;   // 大小写敏感
    options.TrimValues = true;       // 去除空格
});
```

## ?? 高级用法

### 流式处理大文件

```csharp
var csvHelper = new CsvHelper<Person>();

// 逐条处理，内存占用小
await foreach (var person in csvHelper.ReadStreamAsync("huge_file.csv"))
{
    // 处理单条记录
    ProcessPerson(person);
}
```

### 自定义类型转换

```csharp
public class CustomPerson
{
    public int Id { get; set; }
    
    [CsvField(Name = "FullName")]
    public string Name { get; set; }
    
    [CsvField(Format = "yyyy-MM-dd")]
    public DateTime BirthDate { get; set; }
}
```

### JSON 路径查询

```csharp
var jsonHelper = new JsonHelper();

var json = @"{""user"":{""name"":""张三"",""age"":25}}";
var name = jsonHelper.GetValue(json, "$.user.name"); // "张三"
```

### 对象深拷贝

```csharp
var jsonHelper = new JsonHelper();

var original = new Person { Id = 1, Name = "张三" };
var cloned = jsonHelper.DeepClone(original);
```

## ?? 性能优化

### 1. 使用流式读取处理大文件

```csharp
// ? 不推荐：一次性加载所有数据
var all = await csvHelper.ReadAsync("large.csv"); // 占用大量内存

// ? 推荐：流式处理
await foreach (var item in csvHelper.ReadStreamAsync("large.csv"))
{
    // 逐条处理，内存占用小
}
```

### 2. 内存池优化

CsvHelper 内部使用 `ArrayPool<char>` 减少GC压力。

### 3. 批量操作

```csharp
// 批量写入比单条追加更高效
await csvHelper.WriteAsync(filePath, largeDataList);
```

## ?? 示例代码

完整示例请查看：
- `ToolHelperTest/Examples/DataProcessing/DataProcessingExamples.cs`

## ??? 开发指南

### 添加新的文件格式支持

1. 实现 `IFileReader<T>` 和 `IFileWriter<T>` 接口
2. 创建对应的配置类
3. 添加依赖注入扩展方法
4. 编写单元测试和示例

### 代码规范

- 所有公共API必须包含XML注释
- 异步方法必须接受 `CancellationToken`
- 使用 `ILogger` 记录关键操作
- 异常处理要明确清晰

## ?? 更新日志

### v1.0.0 (2026-01-05)
- ? 实现 CsvHelper
- ? 实现 JsonHelper
- ? 实现 XmlHelper
- ? 实现 IniFileHelper
- ? 添加依赖注入支持
- ? 创建示例代码

## ?? 许可证

MIT License

## ?? 贡献

欢迎提交 Issue 和 Pull Request！

## ?? 联系方式

如有问题，请创建 Issue 或联系开发团队。
