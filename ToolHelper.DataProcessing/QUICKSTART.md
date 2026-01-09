# ToolHelper.DataProcessing 快速入门指南

## ?? 5分钟快速开始

### 步骤 1: 添加项目引用

在您的 `.csproj` 文件中添加：

```xml
<ItemGroup>
  <ProjectReference Include="..\ToolHelper.DataProcessing\ToolHelper.DataProcessing.csproj" />
</ItemGroup>
```

或使用命令行：

```bash
dotnet add reference path/to/ToolHelper.DataProcessing.csproj
```

### 步骤 2: 选择使用方式

#### 方式 A: 直接使用（适合简单场景）

```csharp
using ToolHelper.DataProcessing.Csv;
using ToolHelper.DataProcessing.Json;

// CSV 处理
var csvHelper = new CsvHelper<Person>();
await csvHelper.WriteAsync("data.csv", peopleList);
var data = await csvHelper.ReadAsync("data.csv");

// JSON 处理
var jsonHelper = new JsonHelper();
var json = jsonHelper.Serialize(myObject);
var obj = jsonHelper.Deserialize<MyType>(json);
```

#### 方式 B: 依赖注入（推荐用于大型项目）

```csharp
using Microsoft.Extensions.DependencyInjection;
using ToolHelper.DataProcessing.Extensions;

// 注册服务
var services = new ServiceCollection();
services.AddDataProcessing();  // 添加所有基础服务

var serviceProvider = services.BuildServiceProvider();

// 使用服务
var csvHelper = serviceProvider.GetRequiredService<CsvHelper<Person>>();
var jsonHelper = serviceProvider.GetRequiredService<JsonHelper>();
```

### 步骤 3: 运行示例

查看完整示例：

```bash
# 示例文件位置
ToolHelperTest/Examples/DataProcessing/DataProcessingExamples.cs
```

运行示例：

```csharp
using ToolHelperTest.Examples.DataProcessing;

// CSV 示例
await CsvExample.BasicReadWriteAsync();
await CsvExample.StreamReadAsync();

// JSON 示例
JsonExample.SerializeDeserialize();
JsonExample.BeautifyMinify();

// XML 示例
XmlExample.SerializeDeserialize();
XmlExample.XPathQuery();

// INI 示例
await IniExample.ReadWriteAsync();
```

## ?? 常用场景示例

### 场景 1: 导出 CSV 报表

```csharp
public async Task ExportReportAsync(List<SensorData> data)
{
    var csvHelper = new CsvHelper<SensorData>();
    await csvHelper.WriteAsync("sensor_report.csv", data);
    Console.WriteLine("报表已导出到 sensor_report.csv");
}
```

### 场景 2: 读取配置文件

```csharp
public async Task<AppConfig> LoadConfigAsync()
{
    var jsonHelper = new JsonHelper();
    var config = await jsonHelper.DeserializeFromFileAsync<AppConfig>("appsettings.json");
    return config;
}
```

### 场景 3: 处理 INI 配置

```csharp
public async Task UpdateDatabaseConfigAsync()
{
    var iniHelper = new IniFileHelper();
    await iniHelper.LoadAsync("config.ini");
    
    var host = iniHelper.Read("Database", "Host");
    var port = iniHelper.Read<int>("Database", "Port");
    
    Console.WriteLine($"数据库: {host}:{port}");
    
    // 更新配置
    iniHelper.Write("Database", "MaxConnections", 100);
    await iniHelper.SaveAsync("config.ini");
}
```

### 场景 4: 流式处理大文件

```csharp
public async Task ProcessLargeFileAsync(string filePath)
{
    var csvHelper = new CsvHelper<LogEntry>();
    
    int count = 0;
    await foreach (var entry in csvHelper.ReadStreamAsync(filePath))
    {
        // 逐条处理，内存占用低
        ProcessLogEntry(entry);
        count++;
        
        if (count % 10000 == 0)
        {
            Console.WriteLine($"已处理 {count} 条记录");
        }
    }
}
```

### 场景 5: JSON 美化和压缩

```csharp
public void FormatJsonFile()
{
    var jsonHelper = new JsonHelper();
    
    // 读取压缩的JSON
    var compactJson = File.ReadAllText("compact.json");
    
    // 美化输出
    var beautified = jsonHelper.Beautify(compactJson);
    File.WriteAllText("formatted.json", beautified);
    
    // 压缩输出
    var minified = jsonHelper.Minify(beautified);
    File.WriteAllText("minified.json", minified);
}
```

### 场景 6: XML 数据查询

```csharp
public void QueryXmlData()
{
    var xmlHelper = new XmlHelper();
    var xml = File.ReadAllText("data.xml");
    
    // XPath 查询
    var userName = xmlHelper.SelectSingleNode(xml, "//User[@id='1']/Name");
    var allEmails = xmlHelper.SelectNodes(xml, "//User/Email");
    
    Console.WriteLine($"用户名: {userName}");
    Console.WriteLine($"所有邮箱: {string.Join(", ", allEmails)}");
}
```

## ?? 配置选项

### CSV 高级配置

```csharp
services.AddCsvHelper(options =>
{
    options.Delimiter = "\t";          // 使用Tab分隔
    options.HasHeader = true;          // 包含标题行
    options.Encoding = "GB2312";       // 使用GB2312编码
    options.TrimFields = true;         // 自动去除空格
    options.IgnoreEmptyLines = true;   // 忽略空行
    options.StreamBatchSize = 5000;    // 流式批次大小
});
```

### JSON 高级配置

```csharp
services.AddJsonHelper(options =>
{
    options.Indented = true;                      // 格式化输出
    options.IgnoreNullValues = true;              // 忽略null值
    options.PropertyNamingPolicy = "CamelCase";   // 驼峰命名
    options.UseStringEnumConverter = true;        // 枚举转字符串
    options.MaxDepth = 128;                       // 最大深度
});
```

## ?? 故障排除

### 问题 1: 编码问题

**症状**：CSV 文件中文乱码

**解决**：
```csharp
services.AddCsvHelper(options =>
{
    options.Encoding = "GB2312";  // 或 "UTF-8"
    options.AutoDetectEncoding = true;
});
```

### 问题 2: 大文件内存溢出

**症状**：读取大文件时内存占用过高

**解决**：使用流式读取
```csharp
// ? 不要这样
var all = await csvHelper.ReadAsync("huge.csv");

// ? 应该这样
await foreach (var item in csvHelper.ReadStreamAsync("huge.csv"))
{
    // 逐条处理
}
```

### 问题 3: 类型转换失败

**症状**：CSV 读取时字段类型不匹配

**解决**：确保模型属性类型正确
```csharp
public class Person
{
    public int Id { get; set; }           // 数字
    public string Name { get; set; }      // 字符串
    public DateTime BirthDate { get; set; } // 日期
    public double? Salary { get; set; }   // 可空数字
}
```

## ?? 性能建议

### ? 推荐做法

1. **使用流式处理大文件**
   ```csharp
   await foreach (var item in csvHelper.ReadStreamAsync(file)) { }
   ```

2. **复用 Helper 实例**
   ```csharp
   // 使用单例或依赖注入
   services.AddJsonHelper();  // 单例
   ```

3. **批量操作**
   ```csharp
   await csvHelper.WriteAsync(file, largeList);  // 一次写入
   ```

### ? 避免做法

1. **不要频繁创建实例**
   ```csharp
   // ? 不好
   for (int i = 0; i < 1000; i++)
   {
       var helper = new JsonHelper();
   }
   ```

2. **不要一次性加载大文件**
   ```csharp
   // ? 可能导致内存溢出
   var all = await csvHelper.ReadAsync("1GB_file.csv");
   ```

## ?? 进阶主题

### 自定义类型转换

```csharp
public class CustomPerson
{
    public int Id { get; set; }
    
    [Display(Name = "姓名")]
    public string Name { get; set; }
    
    [JsonConverter(typeof(CustomDateConverter))]
    public DateTime Date { get; set; }
}
```

### 并发处理

```csharp
// CsvHelper 是线程安全的
var tasks = fileList.Select(async file =>
{
    var helper = new CsvHelper<Data>();
    return await helper.ReadAsync(file);
});

var results = await Task.WhenAll(tasks);
```

## ?? 获取帮助

- ?? 完整文档：`ToolHelper.DataProcessing/README.md`
- ?? 示例代码：`ToolHelperTest/Examples/DataProcessing/`
- ?? 项目总结：`ToolHelper.DataProcessing/PROJECT_SUMMARY.md`

## ? 下一步

1. ? 尝试运行示例代码
2. ? 根据您的需求定制配置
3. ? 集成到您的项目中
4. ?? 提供反馈和建议

---

**祝您使用愉快！** ??

如有问题，请查看文档或创建 Issue。
