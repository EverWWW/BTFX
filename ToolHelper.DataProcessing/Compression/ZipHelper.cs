using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ToolHelper.DataProcessing.Compression;

/// <summary>
/// ZIP 压缩助手
/// 提供 ZIP 文件的压缩、解压、查看等功能
/// </summary>
/// <example>
/// <code>
/// // 直接创建
/// var zipHelper = new ZipHelper();
/// 
/// // 压缩单个文件
/// await zipHelper.CompressFileAsync("source.txt", "archive.zip");
/// 
/// // 压缩目录
/// await zipHelper.CompressDirectoryAsync("sourceDir", "archive.zip");
/// 
/// // 解压文件
/// await zipHelper.ExtractAsync("archive.zip", "outputDir");
/// 
/// // 查看内容
/// var entries = await zipHelper.GetEntriesAsync("archive.zip");
/// </code>
/// </example>
public class ZipHelper
{
    private readonly ZipOptions _options;
    private readonly ILogger<ZipHelper>? _logger;

    /// <summary>
    /// 创建 ZipHelper 实例
    /// </summary>
    public ZipHelper() : this(Options.Create(new ZipOptions()), null)
    {
    }

    /// <summary>
    /// 创建 ZipHelper 实例
    /// </summary>
    /// <param name="options">ZIP 选项</param>
    /// <param name="logger">日志记录器</param>
    public ZipHelper(IOptions<ZipOptions> options, ILogger<ZipHelper>? logger = null)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 压缩单个文件到 ZIP
    /// </summary>
    /// <param name="sourceFilePath">源文件路径</param>
    /// <param name="zipFilePath">ZIP 文件路径</param>
    /// <param name="entryName">ZIP 中的条目名称，为 null 则使用文件名</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task CompressFileAsync(
        string sourceFilePath,
        string zipFilePath,
        string? entryName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFilePath);
        ArgumentNullException.ThrowIfNull(zipFilePath);

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("源文件不存在", sourceFilePath);
        }

        EnsureDirectoryExists(zipFilePath);

        var actualEntryName = entryName ?? Path.GetFileName(sourceFilePath);

        await Task.Run(async () =>
        {
            using var zipStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None, _options.BufferSize, useAsync: true);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, false, Encoding.GetEncoding(_options.Encoding));

            var entry = archive.CreateEntry(actualEntryName, _options.CompressionLevel);
            await using var entryStream = entry.Open();
            await using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, _options.BufferSize, useAsync: true);
            await sourceStream.CopyToAsync(entryStream, _options.BufferSize, cancellationToken);
        }, cancellationToken);

        _logger?.LogInformation("已压缩文件: {SourceFile} -> {ZipFile}", sourceFilePath, zipFilePath);
    }

    /// <summary>
    /// 压缩多个文件到 ZIP
    /// </summary>
    /// <param name="sourceFilePaths">源文件路径列表</param>
    /// <param name="zipFilePath">ZIP 文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task CompressFilesAsync(
        IEnumerable<string> sourceFilePaths,
        string zipFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceFilePaths);
        ArgumentNullException.ThrowIfNull(zipFilePath);

        var files = sourceFilePaths.ToList();
        if (files.Count == 0)
        {
            throw new ArgumentException("源文件列表不能为空", nameof(sourceFilePaths));
        }

        EnsureDirectoryExists(zipFilePath);

        await Task.Run(async () =>
        {
            using var zipStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None, _options.BufferSize, useAsync: true);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, false, Encoding.GetEncoding(_options.Encoding));

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(filePath))
                {
                    _logger?.LogWarning("跳过不存在的文件: {FilePath}", filePath);
                    continue;
                }

                var entryName = Path.GetFileName(filePath);
                var entry = archive.CreateEntry(entryName, _options.CompressionLevel);
                await using var entryStream = entry.Open();
                await using var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _options.BufferSize, useAsync: true);
                await sourceStream.CopyToAsync(entryStream, _options.BufferSize, cancellationToken);
            }
        }, cancellationToken);

        _logger?.LogInformation("已压缩 {Count} 个文件到: {ZipFile}", files.Count, zipFilePath);
    }

    /// <summary>
    /// 压缩目录到 ZIP
    /// </summary>
    /// <param name="sourceDirectory">源目录路径</param>
    /// <param name="zipFilePath">ZIP 文件路径</param>
    /// <param name="includeBaseDirectory">是否包含基目录</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task CompressDirectoryAsync(
        string sourceDirectory,
        string zipFilePath,
        bool includeBaseDirectory = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceDirectory);
        ArgumentNullException.ThrowIfNull(zipFilePath);

        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"源目录不存在: {sourceDirectory}");
        }

        EnsureDirectoryExists(zipFilePath);

        await Task.Run(() =>
        {
            ZipFile.CreateFromDirectory(
                sourceDirectory,
                zipFilePath,
                _options.CompressionLevel,
                includeBaseDirectory,
                Encoding.GetEncoding(_options.Encoding));
        }, cancellationToken);

        _logger?.LogInformation("已压缩目录: {SourceDir} -> {ZipFile}", sourceDirectory, zipFilePath);
    }

    /// <summary>
    /// 解压 ZIP 文件
    /// </summary>
    /// <param name="zipFilePath">ZIP 文件路径</param>
    /// <param name="destinationDirectory">目标目录</param>
    /// <param name="overwriteFiles">是否覆盖已存在的文件</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ExtractAsync(
        string zipFilePath,
        string destinationDirectory,
        bool overwriteFiles = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipFilePath);
        ArgumentNullException.ThrowIfNull(destinationDirectory);

        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException("ZIP 文件不存在", zipFilePath);
        }

        Directory.CreateDirectory(destinationDirectory);

        await Task.Run(() =>
        {
            ZipFile.ExtractToDirectory(
                zipFilePath,
                destinationDirectory,
                Encoding.GetEncoding(_options.Encoding),
                overwriteFiles);
        }, cancellationToken);

        _logger?.LogInformation("已解压: {ZipFile} -> {DestDir}", zipFilePath, destinationDirectory);
    }

    /// <summary>
    /// 获取 ZIP 文件中的条目列表
    /// </summary>
    /// <param name="zipFilePath">ZIP 文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>条目信息列表</returns>
    public async Task<List<ZipEntryInfo>> GetEntriesAsync(
        string zipFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipFilePath);

        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException("ZIP 文件不存在", zipFilePath);
        }

        return await Task.Run(() =>
        {
            var entries = new List<ZipEntryInfo>();

            using var archive = ZipFile.OpenRead(zipFilePath);
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                entries.Add(new ZipEntryInfo
                {
                    FullName = entry.FullName,
                    Name = entry.Name,
                    IsDirectory = string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/'),
                    Length = entry.Length,
                    CompressedLength = entry.CompressedLength,
                    LastWriteTime = entry.LastWriteTime,
                    Crc32 = entry.Crc32
                });
            }

            return entries;
        }, cancellationToken);
    }

    /// <summary>
    /// 获取 ZIP 文件信息
    /// </summary>
    /// <param name="zipFilePath">ZIP 文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>ZIP 文件信息</returns>
    public async Task<ZipInfo> GetZipInfoAsync(
        string zipFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipFilePath);

        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException("ZIP 文件不存在", zipFilePath);
        }

        var fileInfo = new FileInfo(zipFilePath);
        var entries = await GetEntriesAsync(zipFilePath, cancellationToken);

        return new ZipInfo
        {
            FilePath = zipFilePath,
            FileSize = fileInfo.Length,
            EntryCount = entries.Count,
            TotalUncompressedSize = entries.Sum(e => e.Length),
            TotalCompressedSize = entries.Sum(e => e.CompressedLength),
            CreatedAt = fileInfo.CreationTime
        };
    }

    /// <summary>
    /// 向已存在的 ZIP 文件添加文件
    /// </summary>
    /// <param name="zipFilePath">ZIP 文件路径</param>
    /// <param name="sourceFilePath">要添加的文件路径</param>
    /// <param name="entryName">ZIP 中的条目名称，为 null 则使用文件名</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task AddToArchiveAsync(
        string zipFilePath,
        string sourceFilePath,
        string? entryName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipFilePath);
        ArgumentNullException.ThrowIfNull(sourceFilePath);

        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException("ZIP 文件不存在", zipFilePath);
        }

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("源文件不存在", sourceFilePath);
        }

        var actualEntryName = entryName ?? Path.GetFileName(sourceFilePath);

        await Task.Run(async () =>
        {
            using var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Update, Encoding.GetEncoding(_options.Encoding));

            var existingEntry = archive.GetEntry(actualEntryName);
            existingEntry?.Delete();

            var entry = archive.CreateEntry(actualEntryName, _options.CompressionLevel);
            await using var entryStream = entry.Open();
            await using var sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, _options.BufferSize, useAsync: true);
            await sourceStream.CopyToAsync(entryStream, _options.BufferSize, cancellationToken);
        }, cancellationToken);

        _logger?.LogInformation("已添加文件到 ZIP: {SourceFile} -> {ZipFile}/{EntryName}", sourceFilePath, zipFilePath, actualEntryName);
    }

    /// <summary>
    /// 从 ZIP 文件中删除条目
    /// </summary>
    /// <param name="zipFilePath">ZIP 文件路径</param>
    /// <param name="entryName">要删除的条目名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功删除</returns>
    public async Task<bool> RemoveFromArchiveAsync(
        string zipFilePath,
        string entryName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipFilePath);
        ArgumentNullException.ThrowIfNull(entryName);

        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException("ZIP 文件不存在", zipFilePath);
        }

        return await Task.Run(() =>
        {
            using var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Update, Encoding.GetEncoding(_options.Encoding));
            var entry = archive.GetEntry(entryName);

            if (entry == null)
            {
                return false;
            }

            entry.Delete();
            _logger?.LogInformation("已从 ZIP 删除条目: {ZipFile}/{EntryName}", zipFilePath, entryName);
            return true;
        }, cancellationToken);
    }

    /// <summary>
    /// 从 ZIP 中提取特定条目
    /// </summary>
    /// <param name="zipFilePath">ZIP 文件路径</param>
    /// <param name="entryName">要提取的条目名称</param>
    /// <param name="destinationPath">目标文件路径</param>
    /// <param name="overwrite">是否覆盖已存在的文件</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ExtractEntryAsync(
        string zipFilePath,
        string entryName,
        string destinationPath,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipFilePath);
        ArgumentNullException.ThrowIfNull(entryName);
        ArgumentNullException.ThrowIfNull(destinationPath);

        if (!File.Exists(zipFilePath))
        {
            throw new FileNotFoundException("ZIP 文件不存在", zipFilePath);
        }

        EnsureDirectoryExists(destinationPath);

        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipFilePath);
            var entry = archive.GetEntry(entryName)
                ?? throw new InvalidOperationException($"条目不存在: {entryName}");

            entry.ExtractToFile(destinationPath, overwrite);
        }, cancellationToken);

        _logger?.LogInformation("已提取条目: {ZipFile}/{EntryName} -> {DestPath}", zipFilePath, entryName, destinationPath);
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
