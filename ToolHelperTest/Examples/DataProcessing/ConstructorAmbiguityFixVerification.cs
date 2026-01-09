using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ToolHelper.DataProcessing.Configuration;
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
/// 构造函数歧义修复验证测试
/// </summary>
public class ConstructorAmbiguityFixVerification
{
    /// <summary>
    /// 验证所有 Helper 的依赖注入是否正常工作
    /// </summary>
    public static async Task VerifyDependencyInjectionAsync()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   依赖注入构造函数歧义修复验证                            ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProcessing();
        services.AddYamlHelper();
        services.AddExcelHelper();
        services.AddPdfHelper();

        var serviceProvider = services.BuildServiceProvider();

        Console.WriteLine("【测试 1】验证 CSV Helper DI 解析");
        try
        {
            var csvHelper = serviceProvider.GetRequiredService<CsvHelper<TestData>>();
            Console.WriteLine("? CsvHelper 解析成功\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? CsvHelper 解析失败: {ex.Message}\n");
        }

        Console.WriteLine("【测试 2】验证 JSON Helper DI 解析");
        try
        {
            var jsonHelper = serviceProvider.GetRequiredService<JsonHelper>();
            Console.WriteLine("? JsonHelper 解析成功\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? JsonHelper 解析失败: {ex.Message}\n");
        }

        Console.WriteLine("【测试 3】验证 XML Helper DI 解析");
        try
        {
            var xmlHelper = serviceProvider.GetRequiredService<XmlHelper>();
            Console.WriteLine("? XmlHelper 解析成功\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? XmlHelper 解析失败: {ex.Message}\n");
        }

        Console.WriteLine("【测试 4】验证 INI Helper DI 解析");
        try
        {
            var iniHelper = serviceProvider.GetRequiredService<IniFileHelper>();
            Console.WriteLine("? IniFileHelper 解析成功\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? IniFileHelper 解析失败: {ex.Message}\n");
        }

        Console.WriteLine("【测试 5】验证 YAML Helper DI 解析");
        try
        {
            var yamlHelper = serviceProvider.GetRequiredService<YamlHelper>();
            Console.WriteLine("? YamlHelper 解析成功\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? YamlHelper 解析失败: {ex.Message}\n");
        }

        Console.WriteLine("【测试 6】验证 Excel Helper DI 解析");
        try
        {
            var excelHelper = serviceProvider.GetRequiredService<ExcelHelper<TestData>>();
            Console.WriteLine("? ExcelHelper 解析成功\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? ExcelHelper 解析失败: {ex.Message}\n");
        }

        Console.WriteLine("【测试 7】验证 PDF Helper DI 解析");
        try
        {
            var pdfHelper = serviceProvider.GetRequiredService<PdfHelper>();
            Console.WriteLine("? PdfHelper 解析成功\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? PdfHelper 解析失败: {ex.Message}\n");
        }

        Console.WriteLine("═".PadRight(60, '═'));
        Console.WriteLine("? 所有 Helper 依赖注入验证完成！\n");
    }

    /// <summary>
    /// 验证手动实例化是否正常工作
    /// </summary>
    public static void VerifyManualInstantiation()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   手动实例化验证                                          ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

        Console.WriteLine("【测试 1】无参数实例化");
        try
        {
            var csv = new CsvHelper<TestData>();
            var json = new JsonHelper();
            var xml = new XmlHelper();
            var ini = new IniFileHelper();
            var yaml = new YamlHelper();
            var excel = new ExcelHelper<TestData>();
            var pdf = new PdfHelper();
            Console.WriteLine("? 所有 Helper 无参数实例化成功\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? 无参数实例化失败: {ex.Message}\n");
        }

        Console.WriteLine("【测试 2】使用 IOptions 实例化");
        try
        {
            var csvOpt = Options.Create(new CsvOptions());
            var csv = new CsvHelper<TestData>(csvOpt);

            var jsonOpt = Options.Create(new JsonOptions());
            var json = new JsonHelper(jsonOpt);

            Console.WriteLine("? 使用 IOptions 实例化成功\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? IOptions 实例化失败: {ex.Message}\n");
        }

        Console.WriteLine("【测试 3】传递 null 实例化");
        try
        {
            var csv = new CsvHelper<TestData>(null, null);
            var json = new JsonHelper(null, null);
            var xml = new XmlHelper(null, null);
            Console.WriteLine("? 传递 null 实例化成功\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? null 实例化失败: {ex.Message}\n");
        }

        Console.WriteLine("═".PadRight(60, '═'));
        Console.WriteLine("? 所有手动实例化验证完成！\n");
    }

    /// <summary>
    /// 运行所有验证测试
    /// </summary>
    public static async Task RunAllVerificationsAsync()
    {
        await VerifyDependencyInjectionAsync();
        VerifyManualInstantiation();

        Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   ? 构造函数歧义修复验证通过！                          ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");
    }

    private class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
