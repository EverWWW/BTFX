using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToolHelper.DataProcessing.Csv;
using ToolHelper.DataProcessing.Excel;
using ToolHelper.DataProcessing.Extensions;
using ToolHelper.DataProcessing.Ini;
using ToolHelper.DataProcessing.Json;
using ToolHelper.DataProcessing.Pdf;
using ToolHelper.DataProcessing.Xml;
using ToolHelper.DataProcessing.Yaml;

namespace ToolHelperTest.Examples.DataProcessing;

/// <summary>
/// CSV 文件处理示例
/// 演示CSV文件的读写、流式处理等功能
/// </summary>
public class CsvExample
{
    /// <summary>
    /// 示例 1: 基础CSV读写
    /// </summary>
    public static async Task BasicReadWriteAsync()
    {
        Console.WriteLine("=== CSV 基础读写示例 ===\n");

        // 准备测试数据
        var testData = new List<Person>
        {
            new() { Id = 1, Name = "张三", Age = 25, Email = "zhangsan@example.com" },
            new() { Id = 2, Name = "李四", Age = 30, Email = "lisi@example.com" },
            new() { Id = 3, Name = "王五", Age = 28, Email = "wangwu@example.com" }
        };

        var csvHelper = new CsvHelper<Person>();
        var filePath = "test_data.csv";

        try
        {
            // 写入CSV文件
            Console.WriteLine("写入CSV文件...");
            await csvHelper.WriteAsync(filePath, testData);
            Console.WriteLine($"? 文件已保存: {filePath}\n");

            // 读取CSV文件
            Console.WriteLine("读取CSV文件...");
            var readData = await csvHelper.ReadAsync(filePath);
            
            Console.WriteLine($"? 读取到 {readData.Count()} 条记录:\n");
            foreach (var person in readData)
            {
                Console.WriteLine($"  ID: {person.Id}, 姓名: {person.Name}, 年龄: {person.Age}, 邮箱: {person.Email}");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// 示例 2: 流式读取大文件
    /// </summary>
    public static async Task StreamReadAsync()
    {
        Console.WriteLine("\n=== CSV 流式读取示例 ===\n");

        var csvHelper = new CsvHelper<Person>();
        var filePath = "large_data.csv";

        // 生成大量测试数据
        var largeData = Enumerable.Range(1, 10000)
            .Select(i => new Person
            {
                Id = i,
                Name = $"用户{i}",
                Age = 20 + (i % 50),
                Email = $"user{i}@example.com"
            });

        try
        {
            await csvHelper.WriteAsync(filePath, largeData);
            Console.WriteLine($"? 已生成测试文件（10000条记录）\n");

            Console.WriteLine("开始流式读取...");
            int count = 0;

            await foreach (var person in csvHelper.ReadStreamAsync(filePath))
            {
                count++;
                if (count % 2000 == 0)
                {
                    Console.WriteLine($"  已处理 {count} 条记录...");
                }
            }

            Console.WriteLine($"\n? 流式读取完成，共处理 {count} 条记录");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// 示例 3: 使用依赖注入
    /// </summary>
    public static async Task DependencyInjectionAsync()
    {
        Console.WriteLine("\n=== CSV 依赖注入示例 ===\n");

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        services.AddCsvHelper(options =>
        {
            options.Delimiter = ",";
            options.HasHeader = true;
            options.TrimFields = true;
        });

        var serviceProvider = services.BuildServiceProvider();
        var csvHelper = serviceProvider.GetRequiredService<CsvHelper<Person>>();

        var testData = new List<Person>
        {
            new() { Id = 1, Name = "测试用户", Age = 25, Email = "test@example.com" }
        };

        var filePath = "di_test.csv";

        try
        {
            await csvHelper.WriteAsync(filePath, testData);
            var result = await csvHelper.ReadAsync(filePath);

            Console.WriteLine($"? 通过DI容器处理完成，读取到 {result.Count()} 条记录");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}

/// <summary>
/// JSON 处理示例
/// 演示JSON的序列化、美化、压缩等功能
/// </summary>
public class JsonExample
{
    /// <summary>
    /// 示例 1: JSON 序列化和反序列化
    /// </summary>
    public static void SerializeDeserialize()
    {
        Console.WriteLine("\n=== JSON 序列化/反序列化示例 ===\n");

        var jsonHelper = new JsonHelper();

        var person = new Person
        {
            Id = 1,
            Name = "张三",
            Age = 25,
            Email = "zhangsan@example.com"
        };

        // 序列化
        var json = jsonHelper.Serialize(person);
        Console.WriteLine("序列化结果:");
        Console.WriteLine(json);

        // 反序列化
        var deserialized = jsonHelper.Deserialize<Person>(json);
        Console.WriteLine($"\n? 反序列化成功: {deserialized?.Name}, 年龄 {deserialized?.Age}");
    }

    /// <summary>
    /// 示例 2: JSON 美化和压缩
    /// </summary>
    public static void BeautifyMinify()
    {
        Console.WriteLine("\n=== JSON 美化/压缩示例 ===\n");

        var jsonHelper = new JsonHelper();

        var compactJson = "{\"id\":1,\"name\":\"张三\",\"age\":25}";
        Console.WriteLine("原始JSON:");
        Console.WriteLine(compactJson);

        // 美化
        var beautified = jsonHelper.Beautify(compactJson);
        Console.WriteLine("\n美化后:");
        Console.WriteLine(beautified);

        // 压缩
        var minified = jsonHelper.Minify(beautified);
        Console.WriteLine("\n压缩后:");
        Console.WriteLine(minified);
    }

    /// <summary>
    /// 示例 3: JSON 文件操作
    /// </summary>
    public static async Task FileOperationsAsync()
    {
        Console.WriteLine("\n=== JSON 文件操作示例 ===\n");

        var jsonHelper = new JsonHelper();
        var filePath = "person.json";

        var person = new Person
        {
            Id = 1,
            Name = "李四",
            Age = 30,
            Email = "lisi@example.com"
        };

        try
        {
            // 保存到文件
            await jsonHelper.SerializeToFileAsync(filePath, person);
            Console.WriteLine($"? 已保存到文件: {filePath}");

            // 从文件读取
            var loaded = await jsonHelper.DeserializeFromFileAsync<Person>(filePath);
            Console.WriteLine($"? 从文件加载: {loaded?.Name}, {loaded?.Email}");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}

/// <summary>
/// XML 处理示例
/// 演示XML的序列化、XPath查询等功能
/// </summary>
public class XmlExample
{
    /// <summary>
    /// 示例 1: XML 序列化和反序列化
    /// </summary>
    public static void SerializeDeserialize()
    {
        Console.WriteLine("\n=== XML 序列化/反序列化示例 ===\n");

        var xmlHelper = new XmlHelper();

        var person = new Person
        {
            Id = 1,
            Name = "王五",
            Age = 28,
            Email = "wangwu@example.com"
        };

        // 序列化
        var xml = xmlHelper.Serialize(person);
        Console.WriteLine("序列化结果:");
        Console.WriteLine(xml);

        // 反序列化
        var deserialized = xmlHelper.Deserialize<Person>(xml);
        Console.WriteLine($"\n? 反序列化成功: {deserialized?.Name}");
    }

    /// <summary>
    /// 示例 2: XPath 查询
    /// </summary>
    public static void XPathQuery()
    {
        Console.WriteLine("\n=== XPath 查询示例 ===\n");

        var xmlHelper = new XmlHelper();

        var xml = @"
        <Person>
            <Id>1</Id>
            <Name>赵六</Name>
            <Age>35</Age>
            <Email>zhaoliu@example.com</Email>
        </Person>";

        // 查询单个节点
        var name = xmlHelper.SelectSingleNode(xml, "//Name");
        var age = xmlHelper.SelectSingleNode(xml, "//Age");

        Console.WriteLine($"姓名: {name}");
        Console.WriteLine($"年龄: {age}");
    }
}

/// <summary>
/// INI 文件处理示例
/// 演示INI配置文件的读写操作
/// </summary>
public class IniExample
{
    /// <summary>
    /// 示例 1: INI 文件读写
    /// </summary>
    public static async Task ReadWriteAsync()
    {
        Console.WriteLine("\n=== INI 文件读写示例 ===\n");

        var iniHelper = new IniFileHelper();
        var filePath = "config.ini";

        try
        {
            // 写入配置
            Console.WriteLine("写入配置...");
            iniHelper.Write("Database", "Host", "localhost");
            iniHelper.Write("Database", "Port", "3306");
            iniHelper.Write("Database", "Username", "root");

            iniHelper.Write("Application", "Name", "TestApp");
            iniHelper.Write("Application", "Version", "1.0.0");

            await iniHelper.SaveAsync(filePath);
            Console.WriteLine($"? 配置已保存: {filePath}\n");

            // 读取配置
            Console.WriteLine("读取配置...");
            var newHelper = new IniFileHelper();
            await newHelper.LoadAsync(filePath);

            var host = newHelper.Read("Database", "Host");
            var port = newHelper.Read<int>("Database", "Port");
            var appName = newHelper.Read("Application", "Name");

            Console.WriteLine($"数据库主机: {host}");
            Console.WriteLine($"数据库端口: {port}");
            Console.WriteLine($"应用名称: {appName}");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}


/// <summary>
/// YAML 文件处理示例
/// 演示YAML的序列化和反序列化功能
/// </summary>
public class YamlExample
{
    /// <summary>
    /// 示例 1: YAML 序列化和反序列化
    /// </summary>
    public static async Task SerializeDeserializeAsync()
    {
        Console.WriteLine("\n=== YAML 序列化/反序列化示例 ===\n");

        var yamlHelper = new YamlHelper();

        var config = new AppConfig
        {
            AppName = "测试应用",
            Version = "1.0.0",
            Database = new DatabaseConfig
            {
                Host = "localhost",
                Port = 3306,
                Username = "root",
                Password = "password"
            },
            Features = new List<string> { "功能1", "功能2", "功能3" }
        };

        // 序列化
        var yaml = yamlHelper.Serialize(config);
        Console.WriteLine("序列化结果:");
        Console.WriteLine(yaml);

        // 反序列化
        var deserialized = yamlHelper.Deserialize<AppConfig>(yaml);
        Console.WriteLine($"\n? 反序列化成功: {deserialized?.AppName}, 版本 {deserialized?.Version}");

        // 文件操作
        var filePath = "config.yaml";
        try
        {
            await yamlHelper.SerializeToFileAsync(filePath, config);
            Console.WriteLine($"? 已保存到文件: {filePath}");

            var loaded = await yamlHelper.DeserializeFromFileAsync<AppConfig>(filePath);
            Console.WriteLine($"? 从文件加载: {loaded?.AppName}");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}

/// <summary>
/// Excel 文件处理示例
/// 演示Excel的读写功能
/// </summary>
public class ExcelExample
{
    /// <summary>
    /// 示例 1: Excel 基础读写
    /// </summary>
    public static async Task BasicReadWriteAsync()
    {
        Console.WriteLine("\n=== Excel 基础读写示例 ===\n");

        var testData = new List<Person>
                {
                    new() { Id = 1, Name = "张三", Age = 25, Email = "zhangsan@example.com" },
                    new() { Id = 2, Name = "李四", Age = 30, Email = "lisi@example.com" },
                    new() { Id = 3, Name = "王五", Age = 28, Email = "wangwu@example.com" },
                    new() { Id = 4, Name = "赵六", Age = 32, Email = "zhaoliu@example.com" },
                    new() { Id = 5, Name = "孙七", Age = 27, Email = "sunqi@example.com" }
                };

        var excelHelper = new ExcelHelper<Person>();
        var filePath = "test_data.xlsx";

        try
        {
            // 写入Excel文件
            Console.WriteLine("写入Excel文件...");
            await excelHelper.WriteAsync(filePath, testData);
            Console.WriteLine($"? 文件已保存: {filePath}\n");

            // 读取Excel文件
            Console.WriteLine("读取Excel文件...");
            var readData = await excelHelper.ReadAsync(filePath);

            Console.WriteLine($"? 读取到 {readData.Count()} 条记录:\n");
            foreach (var person in readData)
            {
                Console.WriteLine($"  ID: {person.Id}, 姓名: {person.Name}, 年龄: {person.Age}, 邮箱: {person.Email}");
            }
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// 示例 2: Excel 流式读取
    /// </summary>
    public static async Task StreamReadAsync()
    {
        Console.WriteLine("\n=== Excel 流式读取示例 ===\n");

        var excelHelper = new ExcelHelper<Person>();
        var filePath = "large_data.xlsx";

        // 生成测试数据
        var largeData = Enumerable.Range(1, 1000)
            .Select(i => new Person
            {
                Id = i,
                Name = $"用户{i}",
                Age = 20 + (i % 50),
                Email = $"user{i}@example.com"
            });

        try
        {
            await excelHelper.WriteAsync(filePath, largeData);
            Console.WriteLine($"? 已生成测试文件（1000条记录）\n");

            Console.WriteLine("开始流式读取...");
            int count = 0;

            await foreach (var person in excelHelper.ReadStreamAsync(filePath))
            {
                count++;
                if (count % 200 == 0)
                {
                    Console.WriteLine($"  已处理 {count} 条记录...");
                }
            }

            Console.WriteLine($"\n? 流式读取完成，共处理 {count} 条记录");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}

/// <summary>
/// PDF 文件处理示例
/// 演示PDF的生成功能
/// </summary>
public class PdfExample
{
    /// <summary>
    /// 示例 1: 生成简单文本PDF
    /// </summary>
    public static async Task GenerateTextPdfAsync()
    {
        Console.WriteLine("\n=== PDF 文本生成示例 ===\n");

        var pdfHelper = new PdfHelper();
        var filePath = "text_document.pdf";

        var content = @"这是一个PDF文档示例。

        ToolHelper.DataProcessing 提供了完整的PDF生成功能。

        主要特性：
        1. 支持文本内容
        2. 支持表格数据
        3. 支持报表生成
        4. 自动分页
        5. 页眉页脚

        感谢使用！";

        try
        {
            await pdfHelper.GenerateTextPdfAsync(filePath, content);
            Console.WriteLine($"? 文本PDF已生成: {filePath}");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// 示例 2: 生成表格PDF
    /// </summary>
    public static async Task GenerateTablePdfAsync()
    {
        Console.WriteLine("\n=== PDF 表格生成示例 ===\n");

        var testData = new List<Person>
                {
                    new() { Id = 1, Name = "张三", Age = 25, Email = "zhangsan@example.com" },
                    new() { Id = 2, Name = "李四", Age = 30, Email = "lisi@example.com" },
                    new() { Id = 3, Name = "王五", Age = 28, Email = "wangwu@example.com" },
                    new() { Id = 4, Name = "赵六", Age = 32, Email = "zhaoliu@example.com" },
                    new() { Id = 5, Name = "孙七", Age = 27, Email = "sunqi@example.com" }
                };

        var pdfHelper = new PdfHelper();
        var filePath = "table_report.pdf";

        try
        {
            await pdfHelper.GenerateTablePdfAsync(filePath, testData, "人员信息表");
            Console.WriteLine($"? 表格PDF已生成: {filePath}");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    /// <summary>
    /// 示例 3: 生成完整报表
    /// </summary>
    public static async Task GenerateReportPdfAsync()
    {
        Console.WriteLine("\n=== PDF 报表生成示例 ===\n");

        var testData = Enumerable.Range(1, 20)
            .Select(i => new Person
            {
                Id = i,
                Name = $"员工{i}",
                Age = 20 + (i % 40),
                Email = $"employee{i}@company.com"
            }).ToList();

        var summary = new Dictionary<string, string>
                {
                    { "总人数", testData.Count.ToString() },
                    { "平均年龄", testData.Average(p => p.Age).ToString("F1") },
                    { "最大年龄", testData.Max(p => p.Age).ToString() },
                    { "最小年龄", testData.Min(p => p.Age).ToString() }
                };

        var pdfHelper = new PdfHelper();
        var filePath = "employee_report.pdf";

        try
        {
            await pdfHelper.GenerateReportPdfAsync(filePath, testData, "员工信息统计报表", summary);
            Console.WriteLine($"? 完整报表已生成: {filePath}");
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}

/// <summary>
/// 测试数据模型
/// </summary>
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// 应用配置模型
/// </summary>
public class AppConfig
{
    public string AppName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DatabaseConfig Database { get; set; } = new();
    public List<string> Features { get; set; } = new();
}

/// <summary>
/// 数据库配置模型
/// </summary>
public class DatabaseConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
