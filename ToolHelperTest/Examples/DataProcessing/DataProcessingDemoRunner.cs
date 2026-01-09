using ToolHelperTest.Examples.DataProcessing;

namespace ToolHelperTest.Examples;

/// <summary>
/// 数据处理完整示例演示程序
/// </summary>
public class DataProcessingDemoRunner
{
    public static async Task RunAllExamplesAsync()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   ToolHelper.DataProcessing 完整功能演示                  ║");
        Console.WriteLine("║   包含: CSV, JSON, XML, INI, YAML, Excel, PDF            ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════╝\n");

        try
        {
            // CSV 示例
            Console.WriteLine("【1/7】CSV 文件处理");
            Console.WriteLine("═".PadRight(60, '═'));
            await CsvExample.BasicReadWriteAsync();
            await CsvExample.StreamReadAsync();
            await CsvExample.DependencyInjectionAsync();

            // JSON 示例
            Console.WriteLine("\n【2/7】JSON 处理");
            Console.WriteLine("═".PadRight(60, '═'));
            JsonExample.SerializeDeserialize();
            JsonExample.BeautifyMinify();
            await JsonExample.FileOperationsAsync();

            // XML 示例
            Console.WriteLine("\n【3/7】XML 处理");
            Console.WriteLine("═".PadRight(60, '═'));
            XmlExample.SerializeDeserialize();
            XmlExample.XPathQuery();

            // INI 示例
            Console.WriteLine("\n【4/7】INI 配置文件");
            Console.WriteLine("═".PadRight(60, '═'));
            await IniExample.ReadWriteAsync();

            // YAML 示例
            Console.WriteLine("\n【5/7】YAML 配置文件");
            Console.WriteLine("═".PadRight(60, '═'));
            await YamlExample.SerializeDeserializeAsync();

            // Excel 示例
            Console.WriteLine("\n【6/7】Excel 文件处理");
            Console.WriteLine("═".PadRight(60, '═'));
            await ExcelExample.BasicReadWriteAsync();
            await ExcelExample.StreamReadAsync();

            // PDF 示例
            Console.WriteLine("\n【7/7】PDF 文件生成");
            Console.WriteLine("═".PadRight(60, '═'));
            await PdfExample.GenerateTextPdfAsync();
            await PdfExample.GenerateTablePdfAsync();
            await PdfExample.GenerateReportPdfAsync();

            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   ? 所有示例执行完成！                                   ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════╝");

            Console.WriteLine("\n?? 功能总结:");
            Console.WriteLine("  ? CSV    - 读写、流式处理");
            Console.WriteLine("  ? JSON   - 序列化、美化、压缩");
            Console.WriteLine("  ? XML    - 序列化、XPath查询");
            Console.WriteLine("  ? INI    - 配置文件管理");
            Console.WriteLine("  ? YAML   - 配置序列化");
            Console.WriteLine("  ? Excel  - 读写、样式支持");
            Console.WriteLine("  ? PDF    - 文本、表格、报表生成");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n? 发生错误: {ex.Message}");
            Console.WriteLine($"   {ex.StackTrace}");
        }

        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
}
