using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BTFX.Common;
using BTFX.Data;
using BTFX.Models;
using BTFX.Models.Analysis;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// Creates and validates software-readable analysis result packages.
/// </summary>
public sealed class AnalysisPackageService : IAnalysisPackageService
{
    private readonly ILogHelper? _logHelper;

    public AnalysisPackageService(ILogHelper? logHelper = null)
    {
        _logHelper = logHelper;
    }

    public async Task<AnalysisPackageOperationResult> CreatePackageAsync(
        AnalysisResult result,
        MeasurementRecord? measurement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Id <= 0)
        {
            return new AnalysisPackageOperationResult
            {
                Success = false,
                Message = "分析结果尚未入库，无法生成结果包。"
            };
        }

        if (string.IsNullOrWhiteSpace(result.OutputDirectory) || !Directory.Exists(result.OutputDirectory))
        {
            return new AnalysisPackageOperationResult
            {
                Success = false,
                Message = "分析输出目录不存在，无法生成结果包。"
            };
        }

        try
        {
            var packageDirectory = Path.Combine(result.OutputDirectory, "package");
            Directory.CreateDirectory(packageDirectory);

            var safeRequestId = MakeSafeFileName(string.IsNullOrWhiteSpace(result.RequestId)
                ? $"analysis_{result.Id}"
                : result.RequestId);
            var packagePath = Path.Combine(packageDirectory, $"{safeRequestId}.btfxpkg");

            if (File.Exists(packagePath))
            {
                File.Delete(packagePath);
            }

            var files = await CollectFilesAsync(result, cancellationToken);
            var manifest = BuildManifest(result, measurement, files);
            var checksums = files.ToDictionary(file => file.EntryName, file => file.Sha256);

            await using (var stream = new FileStream(packagePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                await AddJsonEntryAsync(archive, "manifest.json", manifest, cancellationToken);
                await AddJsonEntryAsync(archive, "checksums.json", checksums, cancellationToken);

                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    archive.CreateEntryFromFile(file.SourcePath, file.EntryName, CompressionLevel.Fastest);
                }
            }

            result.PackagePath = packagePath;
            result.PackageCreatedAt = DateTime.Now;
            result.PackageValidationStatus = "Valid";
            result.PackageValidationMessage = "结果包已生成";
            await UpdatePackageInfoAsync(result, cancellationToken);

            _logHelper?.Information($"分析结果包生成成功: AnalysisResultId={result.Id}, Path={packagePath}");

            return new AnalysisPackageOperationResult
            {
                Success = true,
                PackagePath = packagePath,
                Message = "结果包已生成"
            };
        }
        catch (Exception ex)
        {
            result.PackageValidationStatus = "Error";
            result.PackageValidationMessage = ex.Message;
            await UpdatePackageInfoAsync(result, cancellationToken);

            _logHelper?.Error($"分析结果包生成失败: AnalysisResultId={result.Id}", ex);
            return new AnalysisPackageOperationResult
            {
                Success = false,
                Message = $"结果包生成失败: {ex.Message}"
            };
        }
    }

    public async Task<AnalysisPackageValidationResult> ValidatePackageAsync(
        AnalysisResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (string.IsNullOrWhiteSpace(result.PackagePath))
        {
            return new AnalysisPackageValidationResult
            {
                IsValid = true,
                Message = "当前分析结果暂无结果包，已跳过包校验。"
            };
        }

        if (!File.Exists(result.PackagePath))
        {
            result.PackageValidationStatus = "Missing";
            result.PackageValidationMessage = "结果包文件不存在";
            await UpdatePackageInfoAsync(result, cancellationToken);

            return new AnalysisPackageValidationResult
            {
                IsValid = false,
                Message = "结果包文件不存在，请重新分析。"
            };
        }

        try
        {
            using var archive = ZipFile.OpenRead(result.PackagePath);
            var checksumEntry = archive.GetEntry("checksums.json");
            if (checksumEntry is null)
            {
                result.PackageValidationStatus = "Invalid";
                result.PackageValidationMessage = "结果包缺少 checksums.json";
                await UpdatePackageInfoAsync(result, cancellationToken);
                return new AnalysisPackageValidationResult { IsValid = false, Message = result.PackageValidationMessage };
            }

            Dictionary<string, string>? checksums;
            await using (var checksumStream = checksumEntry.Open())
            {
                checksums = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(checksumStream, cancellationToken: cancellationToken);
            }

            if (checksums is null || checksums.Count == 0)
            {
                result.PackageValidationStatus = "Invalid";
                result.PackageValidationMessage = "结果包校验信息为空";
                await UpdatePackageInfoAsync(result, cancellationToken);
                return new AnalysisPackageValidationResult { IsValid = false, Message = result.PackageValidationMessage };
            }

            foreach (var (entryName, expectedHash) in checksums)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entry = archive.GetEntry(entryName);
                if (entry is null)
                {
                    result.PackageValidationStatus = "Invalid";
                    result.PackageValidationMessage = $"结果包缺少文件: {entryName}";
                    await UpdatePackageInfoAsync(result, cancellationToken);
                    return new AnalysisPackageValidationResult { IsValid = false, Message = result.PackageValidationMessage };
                }

                await using var entryStream = entry.Open();
                var actualHash = await ComputeSha256Async(entryStream, cancellationToken);
                if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    result.PackageValidationStatus = "Invalid";
                    result.PackageValidationMessage = $"结果包文件校验失败: {entryName}";
                    await UpdatePackageInfoAsync(result, cancellationToken);
                    return new AnalysisPackageValidationResult { IsValid = false, Message = result.PackageValidationMessage };
                }
            }

            result.PackageValidationStatus = "Valid";
            result.PackageValidationMessage = "结果包校验通过";
            await UpdatePackageInfoAsync(result, cancellationToken);
            return new AnalysisPackageValidationResult { IsValid = true, Message = result.PackageValidationMessage };
        }
        catch (Exception ex)
        {
            result.PackageValidationStatus = "Invalid";
            result.PackageValidationMessage = $"结果包校验异常: {ex.Message}";
            await UpdatePackageInfoAsync(result, cancellationToken);

            _logHelper?.Error($"分析结果包校验失败: AnalysisResultId={result.Id}", ex);
            return new AnalysisPackageValidationResult { IsValid = false, Message = result.PackageValidationMessage };
        }
    }

    private static async Task<List<AnalysisPackageFile>> CollectFilesAsync(
        AnalysisResult result,
        CancellationToken cancellationToken)
    {
        var candidates = new List<(string? Path, string EntryPrefix)>
        {
            (Path.Combine(result.OutputDirectory, "result.json"), "analysis"),
            (Path.Combine(result.OutputDirectory, "summary.json"), "analysis"),
            (result.SummaryFilePath, "analysis"),
            (result.AnnotatedVideoPath, "media"),
            (Path.Combine(result.OutputDirectory, "log.txt"), "logs")
        };

        if (result.CsvFiles is not null)
        {
            foreach (var csv in result.CsvFiles)
            {
                candidates.Add((csv.FilePath, "analysis"));
            }
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new List<AnalysisPackageFile>();

        foreach (var (path, entryPrefix) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(path);
            if (!seen.Add(fullPath))
            {
                continue;
            }

            var fileInfo = new FileInfo(fullPath);
            var entryName = $"{entryPrefix}/{fileInfo.Name}".Replace('\\', '/');
            await using var stream = File.OpenRead(fullPath);

            files.Add(new AnalysisPackageFile
            {
                EntryName = entryName,
                SourcePath = fullPath,
                Size = fileInfo.Length,
                Sha256 = await ComputeSha256Async(stream, cancellationToken)
            });
        }

        return files;
    }

    private static AnalysisPackageManifest BuildManifest(
        AnalysisResult result,
        MeasurementRecord? measurement,
        List<AnalysisPackageFile> files)
    {
        return new AnalysisPackageManifest
        {
            AppVersion = typeof(App).Assembly.GetName().Version?.ToString() ?? "1.0",
            MeasurementId = result.MeasurementId,
            MeasurementName = measurement?.MeasurementName,
            PatientId = measurement?.PatientId ?? 0,
            PatientName = measurement?.Patient?.Name,
            AnalysisResultId = result.Id,
            RequestId = result.RequestId,
            TaskStatus = result.TaskStatus,
            Success = result.Success,
            OutputDirectory = result.OutputDirectory,
            SideVideoPath = measurement?.SideVideoPath,
            SideVideoSize = TryGetFileSize(measurement?.SideVideoPath),
            SideVideoModifiedAt = TryGetLastWriteTime(measurement?.SideVideoPath),
            FrontVideoPath = measurement?.FrontVideoPath,
            FrontVideoSize = TryGetFileSize(measurement?.FrontVideoPath),
            FrontVideoModifiedAt = TryGetLastWriteTime(measurement?.FrontVideoPath),
            Files = files
        };
    }

    private async Task UpdatePackageInfoAsync(AnalysisResult result, CancellationToken cancellationToken)
    {
        using var db = DatabaseFactory.CreateSqliteSugarHelper();
        await db.UpdateAsync<AnalysisResult>(
            r => new AnalysisResult
            {
                PackagePath = result.PackagePath,
                PackageCreatedAt = result.PackageCreatedAt,
                PackageValidationStatus = result.PackageValidationStatus,
                PackageValidationMessage = result.PackageValidationMessage
            },
            r => r.Id == result.Id);
    }

    private static async Task AddJsonEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        T value,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }

    private static async Task<string> ComputeSha256Async(Stream stream, CancellationToken cancellationToken)
    {
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static long? TryGetFileSize(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? new FileInfo(path).Length
            : null;
    }

    private static DateTime? TryGetLastWriteTime(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? File.GetLastWriteTime(path)
            : null;
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
