using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToolHelper.DataProcessing.Compression;
using ToolHelper.DataProcessing.Extensions;

namespace ToolHelperTest.Examples.DataProcessing;

/// <summary>
/// ZIP 压缩示例
/// 演示ZIP文件的压缩、解压、管理等功能
/// </summary>
public class ZipExample
{
    private static readonly string TestDirectory = "zip_test";
    private static readonly string TestZipFile = "test_archive.zip";

    /// <summary>
    /// 运行所有ZIP示例
    /// </summary>
    public static async Task RunAllExamplesAsync()
    {
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║      ZIP 压缩工具示例                ║");
        Console.WriteLine("╚══════════════════════════════════════╝\n");

        try
        {
            // 准备测试环境
            await PrepareTestEnvironmentAsync();

            // 示例1: 压缩单个文件
            await CompressSingleFileAsync();

            // 示例2: 压缩多个文件
            await CompressMultipleFilesAsync();

            // 示例3: 压缩整个文件夹
            await CompressDirectoryAsync();

            // 示例4: 解压文件
            await ExtractArchiveAsync();

            // 示例5: 查看ZIP内容
            await ViewArchiveContentsAsync();

            // 示例6: 向现有ZIP添加文件
            await AddToArchiveAsync();

            // 示例7: 使用依赖注入
            await DependencyInjectionExampleAsync();
        }
        finally
        {
            // 清理测试环境
            CleanupTestEnvironment();
        }
    }

    /// <summary>
    /// 准备测试环境
    /// </summary>
    private static async Task PrepareTestEnvironmentAsync()
    {
        Console.WriteLine("?? 准备测试环境...\n");

        // 创建测试目录
        Directory.CreateDirectory(TestDirectory);
        Directory.CreateDirectory(Path.Combine(TestDirectory, "source"));
        Directory.CreateDirectory(Path.Combine(TestDirectory, "source", "subdir"));
        Directory.CreateDirectory(Path.Combine(TestDirectory, "output"));

        // 创建测试文件
        await File.WriteAllTextAsync(
            Path.Combine(TestDirectory, "source", "file1.txt"),
            "这是第一个测试文件的内容。\n包含多行文本。",
            Encoding.UTF8);

        await File.WriteAllTextAsync(
            Path.Combine(TestDirectory, "source", "file2.txt"),
            "这是第二个测试文件。",
            Encoding.UTF8);

        await File.WriteAllTextAsync(
            Path.Combine(TestDirectory, "source", "data.json"),
            "{ \"name\": \"测试数据\", \"value\": 123 }",
            Encoding.UTF8);

        await File.WriteAllTextAsync(
            Path.Combine(TestDirectory, "source", "subdir", "nested.txt"),
            "这是嵌套目录中的文件。",
            Encoding.UTF8);

        Console.WriteLine("? 测试环境准备完成\n");
    }

    /// <summary>
    /// 示例1: 压缩单个文件
    /// </summary>
    private static async Task CompressSingleFileAsync()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例1: 压缩单个文件");
        Console.WriteLine("═══════════════════════════════════════\n");

        var zipHelper = new ZipHelper();
        var sourceFile = Path.Combine(TestDirectory, "source", "file1.txt");
        var zipFile = Path.Combine(TestDirectory, "output", "single_file.zip");

        await zipHelper.CompressFileAsync(sourceFile, zipFile);

        var zipInfo = await zipHelper.GetZipInfoAsync(zipFile);
        Console.WriteLine($"? 压缩完成!");
        Console.WriteLine($"   源文件: {sourceFile}");
        Console.WriteLine($"   ZIP文件: {zipFile}");
        Console.WriteLine($"   ZIP大小: {zipInfo.FileSize} 字节");
        Console.WriteLine($"   压缩率: {zipInfo.CompressionRatio:F1}%\n");
    }

    /// <summary>
    /// 示例2: 压缩多个文件
    /// </summary>
    private static async Task CompressMultipleFilesAsync()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例2: 压缩多个文件");
        Console.WriteLine("═══════════════════════════════════════\n");

        var zipHelper = new ZipHelper();
        var sourceDir = Path.Combine(TestDirectory, "source");
        var files = new[]
        {
            Path.Combine(sourceDir, "file1.txt"),
            Path.Combine(sourceDir, "file2.txt"),
            Path.Combine(sourceDir, "data.json")
        };
        var zipFile = Path.Combine(TestDirectory, "output", "multiple_files.zip");

        await zipHelper.CompressFilesAsync(files, zipFile);

        var zipInfo = await zipHelper.GetZipInfoAsync(zipFile);
        Console.WriteLine($"? 压缩完成!");
        Console.WriteLine($"   文件数量: {files.Length}");
        Console.WriteLine($"   ZIP文件: {zipFile}");
        Console.WriteLine($"   条目数量: {zipInfo.EntryCount}");
        Console.WriteLine($"   总大小: {zipInfo.FileSize} 字节\n");
    }

    /// <summary>
    /// 示例3: 压缩整个文件夹
    /// </summary>
    private static async Task CompressDirectoryAsync()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例3: 压缩整个文件夹");
        Console.WriteLine("═══════════════════════════════════════\n");

        var zipHelper = new ZipHelper();
        var sourceDir = Path.Combine(TestDirectory, "source");
        var zipFile = Path.Combine(TestDirectory, "output", "directory.zip");

        await zipHelper.CompressDirectoryAsync(sourceDir, zipFile, includeBaseDirectory: false);

        var zipInfo = await zipHelper.GetZipInfoAsync(zipFile);
        Console.WriteLine($"? 文件夹压缩完成!");
        Console.WriteLine($"   源目录: {sourceDir}");
        Console.WriteLine($"   ZIP文件: {zipFile}");
        Console.WriteLine($"   条目数量: {zipInfo.EntryCount}");
        Console.WriteLine($"   原始大小: {zipInfo.TotalUncompressedSize} 字节");
        Console.WriteLine($"   压缩大小: {zipInfo.TotalCompressedSize} 字节");
        Console.WriteLine($"   压缩率: {zipInfo.CompressionRatio:F1}%\n");
    }

    /// <summary>
    /// 示例4: 解压文件
    /// </summary>
    private static async Task ExtractArchiveAsync()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例4: 解压文件");
        Console.WriteLine("═══════════════════════════════════════\n");

        var zipHelper = new ZipHelper();
        var zipFile = Path.Combine(TestDirectory, "output", "directory.zip");
        var extractDir = Path.Combine(TestDirectory, "output", "extracted");

        await zipHelper.ExtractAsync(zipFile, extractDir);

        var extractedFiles = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories);
        Console.WriteLine($"? 解压完成!");
        Console.WriteLine($"   ZIP文件: {zipFile}");
        Console.WriteLine($"   解压目录: {extractDir}");
        Console.WriteLine($"   解压文件数: {extractedFiles.Length}");
        
        foreach (var file in extractedFiles)
        {
            var relativePath = Path.GetRelativePath(extractDir, file);
            Console.WriteLine($"      - {relativePath}");
        }
        Console.WriteLine();
    }

    /// <summary>
    /// 示例5: 查看ZIP内容
    /// </summary>
    private static async Task ViewArchiveContentsAsync()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例5: 查看ZIP内容");
        Console.WriteLine("═══════════════════════════════════════\n");

        var zipHelper = new ZipHelper();
        var zipFile = Path.Combine(TestDirectory, "output", "directory.zip");

        var entries = await zipHelper.GetEntriesAsync(zipFile);

        Console.WriteLine($"ZIP文件: {zipFile}");
        Console.WriteLine("\n条目列表:");
        Console.WriteLine("┌──────────────────────────┬─────────────┬─────────────┬──────────┐");
        Console.WriteLine("│ 名称                     │ 原始大小    │ 压缩大小    │ 压缩率   │");
        Console.WriteLine("├──────────────────────────┼─────────────┼─────────────┼──────────┤");

        foreach (var entry in entries)
        {
            var name = entry.FullName.PadRight(24);
            if (name.Length > 24) name = name.Substring(0, 21) + "...";
            
            Console.WriteLine($"│ {name} │ {entry.Length,8} B  │ {entry.CompressedLength,8} B  │ {entry.CompressionRatio,6:F1}% │");
        }

        Console.WriteLine("└──────────────────────────┴─────────────┴─────────────┴──────────┘\n");
    }

    /// <summary>
    /// 示例6: 向现有ZIP添加文件
    /// </summary>
    private static async Task AddToArchiveAsync()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例6: 向现有ZIP添加文件");
        Console.WriteLine("═══════════════════════════════════════\n");

        var zipHelper = new ZipHelper();
        var zipFile = Path.Combine(TestDirectory, "output", "single_file.zip");

        // 创建一个新文件
        var newFile = Path.Combine(TestDirectory, "source", "new_file.txt");
        await File.WriteAllTextAsync(newFile, "这是新添加的文件内容。", Encoding.UTF8);

        // 获取添加前的信息
        var beforeInfo = await zipHelper.GetZipInfoAsync(zipFile);
        Console.WriteLine($"添加前条目数: {beforeInfo.EntryCount}");

        // 添加文件到ZIP
        await zipHelper.AddToArchiveAsync(zipFile, newFile, "added/new_file.txt");

        // 获取添加后的信息
        var afterInfo = await zipHelper.GetZipInfoAsync(zipFile);
        Console.WriteLine($"添加后条目数: {afterInfo.EntryCount}");
        Console.WriteLine($"? 文件添加完成!\n");
    }

    /// <summary>
    /// 示例7: 使用依赖注入
    /// </summary>
    private static async Task DependencyInjectionExampleAsync()
    {
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("示例7: 使用依赖注入");
        Console.WriteLine("═══════════════════════════════════════\n");

        var services = new ServiceCollection();
        
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Information));
        
        services.AddZipHelper(options =>
        {
            options.CompressionLevel = System.IO.Compression.CompressionLevel.Fastest;
        });

        var serviceProvider = services.BuildServiceProvider();
        var zipHelper = serviceProvider.GetRequiredService<ZipHelper>();

        var sourceFile = Path.Combine(TestDirectory, "source", "file1.txt");
        var zipFile = Path.Combine(TestDirectory, "output", "di_test.zip");

        await zipHelper.CompressFileAsync(sourceFile, zipFile);

        Console.WriteLine($"? 通过依赖注入使用ZipHelper成功!");
        Console.WriteLine($"   ZIP文件: {zipFile}\n");
    }

    /// <summary>
    /// 清理测试环境
    /// </summary>
    private static void CleanupTestEnvironment()
    {
        Console.WriteLine("?? 清理测试环境...");

        try
        {
            if (Directory.Exists(TestDirectory))
            {
                Directory.Delete(TestDirectory, true);
            }
            Console.WriteLine("? 清理完成\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? 清理失败: {ex.Message}\n");
        }
    }
}
