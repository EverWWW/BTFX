# ?? ToolHelper.DataProcessing 快速参考卡

## ?? 一览表

| Helper | 格式 | 主要用途 | 代码示例 |
|--------|------|----------|----------|
| **CsvHelper** | CSV | 数据导入导出 | `await csvHelper.WriteAsync("data.csv", list)` |
| **JsonHelper** | JSON | API、配置 | `var json = jsonHelper.Serialize(obj)` |
| **XmlHelper** | XML | 传统配置、SOAP | `var value = xmlHelper.SelectSingleNode(xml, xpath)` |
| **IniFileHelper** | INI | 简单配置 | `await iniHelper.LoadAsync("config.ini")` |
| **YamlHelper** | YAML | 现代配置 | `var config = yamlHelper.Deserialize<T>(yaml)` |
| **ExcelHelper** | Excel | 报表、数据分析 | `await excelHelper.WriteAsync("report.xlsx", data)` |
| **PdfHelper** | PDF | 报告生成 | `await pdfHelper.GenerateReportPdfAsync(path, data, title)` |

## ?? 选择指南

### 配置文件？
- **简单键值** → `IniFileHelper`
- **层级结构** → `JsonHelper` 或 `YamlHelper`
- **传统系统** → `XmlHelper`

### 数据交换？
- **Web API** → `JsonHelper`
- **跨平台** → `JsonHelper` 或 `XmlHelper`
- **表格数据** → `CsvHelper` 或 `ExcelHelper`

### 报表输出？
- **纯数据** → `CsvHelper` 或 `ExcelHelper`
- **打印文档** → `PdfHelper`
- **数据分析** → `ExcelHelper`

## ?? 代码速查

### CSV - 快速读写
```csharp
var csv = new CsvHelper<Person>();
await csv.WriteAsync("data.csv", people);        // 写入
var data = await csv.ReadAsync("data.csv");      // 读取

// 大文件流式处理
await foreach (var item in csv.ReadStreamAsync("huge.csv"))
{
    Process(item);
}
```

### JSON - 序列化与美化
```csharp
var json = new JsonHelper();
var str = json.Serialize(obj);                   // 序列化
var obj = json.Deserialize<T>(str);              // 反序列化
var pretty = json.Beautify(compactJson);         // 美化
var compact = json.Minify(prettyJson);           // 压缩
```

### XML - 查询与解析
```csharp
var xml = new XmlHelper();
var str = xml.Serialize(obj);                    // 序列化
var obj = xml.Deserialize<T>(str);               // 反序列化
var val = xml.SelectSingleNode(str, "//User");   // XPath查询
```

### INI - 配置管理
```csharp
var ini = new IniFileHelper();
await ini.LoadAsync("config.ini");               // 加载
var host = ini.Read("Database", "Host");         // 读取
ini.Write("Database", "Port", 3306);             // 写入
await ini.SaveAsync("config.ini");               // 保存
```

### YAML - 配置序列化
```csharp
var yaml = new YamlHelper();
var str = yaml.Serialize(config);                // 序列化
var cfg = yaml.Deserialize<Config>(str);         // 反序列化
await yaml.SerializeToFileAsync("app.yaml", cfg);// 保存到文件
```

### Excel - 数据处理
```csharp
var excel = new ExcelHelper<Person>();
await excel.WriteAsync("data.xlsx", people);     // 写入
var data = await excel.ReadAsync("data.xlsx");   // 读取

// 流式读取大文件
await foreach (var row in excel.ReadStreamAsync("large.xlsx"))
{
    Process(row);
}
```

### PDF - 报表生成
```csharp
var pdf = new PdfHelper();

// 文本PDF
await pdf.GenerateTextPdfAsync("doc.pdf", content);

// 表格PDF
await pdf.GenerateTablePdfAsync("table.pdf", data, "标题");

// 完整报表
var summary = new Dictionary<string, string> { {"总计", "100"} };
await pdf.GenerateReportPdfAsync("report.pdf", data, "报表", summary);
```

## ?? 依赖注入

### 注册所有服务
```csharp
services.AddDataProcessing();  // 全部注册
```

### 单独注册
```csharp
services.AddCsvHelper(opt => opt.Delimiter = ",");
services.AddJsonHelper(opt => opt.Indented = true);
services.AddXmlHelper();
services.AddIniHelper();
services.AddYamlHelper();
services.AddExcelHelper();
services.AddPdfHelper();
```

### 使用服务
```csharp
public class MyService
{
    private readonly CsvHelper<Person> _csvHelper;
    private readonly JsonHelper _jsonHelper;
    
    public MyService(
        CsvHelper<Person> csvHelper,
        JsonHelper jsonHelper)
    {
        _csvHelper = csvHelper;
        _jsonHelper = jsonHelper;
    }
}
```

## ?? 配置选项

### CSV配置
```csharp
services.AddCsvHelper(options =>
{
    options.Delimiter = ",";           // 分隔符
    options.HasHeader = true;          // 包含标题
    options.Encoding = "UTF-8";        // 编码
    options.TrimFields = true;         // 去除空格
});
```

### JSON配置
```csharp
services.AddJsonHelper(options =>
{
    options.Indented = true;                   // 格式化
    options.PropertyNamingPolicy = "CamelCase"; // 命名策略
    options.IgnoreNullValues = true;           // 忽略null
});
```

### Excel配置
```csharp
services.AddExcelHelper(options =>
{
    options.SheetName = "Sheet1";      // 工作表名
    options.HasHeader = true;          // 包含标题
    options.AutoSizeColumns = true;    // 自动列宽
});
```

### PDF配置
```csharp
services.AddPdfHelper(options =>
{
    options.PageSize = "A4";           // 页面大小
    options.Margin = 20;               // 页边距(mm)
    options.IncludePageNumbers = true; // 页码
    options.FontSize = 12;             // 字体大小
});
```

## ?? 常见场景

### 场景1: 数据导出报表
```csharp
// 1. 查询数据
var data = await GetDataAsync();

// 2. 导出Excel
var excel = new ExcelHelper<DataRow>();
await excel.WriteAsync("report.xlsx", data);

// 3. 生成PDF报表
var pdf = new PdfHelper();
await pdf.GenerateReportPdfAsync("report.pdf", data, "月度报表");
```

### 场景2: 配置文件管理
```csharp
// 读取YAML配置
var yaml = new YamlHelper();
var config = await yaml.DeserializeFromFileAsync<AppConfig>("appsettings.yaml");

// 或使用INI
var ini = new IniFileHelper();
await ini.LoadAsync("config.ini");
var dbHost = ini.Read("Database", "Host");
```

### 场景3: API数据交换
```csharp
// 序列化发送
var json = new JsonHelper();
var jsonString = json.Serialize(requestData);
await SendToApiAsync(jsonString);

// 接收反序列化
var response = await GetFromApiAsync();
var result = json.Deserialize<ResponseData>(response);
```

### 场景4: 大文件处理
```csharp
// CSV流式处理
var csv = new CsvHelper<LogEntry>();
await foreach (var log in csv.ReadStreamAsync("huge_log.csv"))
{
    await ProcessLogAsync(log);
}

// Excel流式处理
var excel = new ExcelHelper<Record>();
await foreach (var record in excel.ReadStreamAsync("large_data.xlsx"))
{
    await ProcessRecordAsync(record);
}
```

## ?? 性能提示

### ? 推荐做法
- 使用流式处理大文件 (`ReadStreamAsync`)
- 复用Helper实例（通过DI）
- 批量操作代替单条处理

### ? 避免做法
- 一次性加载超大文件 (`ReadAsync`)
- 频繁创建Helper实例
- 在循环中重复打开文件

## ?? 故障排除

### 编码问题
```csharp
services.AddCsvHelper(opt => opt.Encoding = "GB2312");
```

### 内存溢出
```csharp
// 改用流式读取
await foreach (var item in helper.ReadStreamAsync(file)) { }
```

### 类型转换失败
```csharp
// 确保属性类型匹配
public class Person
{
    public int Id { get; set; }        // 数字
    public string Name { get; set; }   // 字符串
    public DateTime? Date { get; set; } // 可空日期
}
```

## ?? 更多资源

- ?? 完整文档: `README.md`
- ?? 快速入门: `QUICKSTART.md`
- ?? 示例代码: `Examples/DataProcessing/`
- ?? 更新日志: `V2.0_COMPLETION_REPORT.md`

---

**版本**: v2.0.0  
**最后更新**: 2026-01-05  
**许可证**: MIT License

**祝您使用愉快！** ??
