using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace BTFX.Services.Implementations;

internal static class ArchiveImportFileStager
{
    public static string ResolveSafePath(string root, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        var rootFullPath = Path.GetFullPath(root);
        var targetFullPath = Path.GetFullPath(Path.Combine(
            rootFullPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
        var relativeToRoot = Path.GetRelativePath(rootFullPath, targetFullPath);

        if (Path.IsPathRooted(relativeToRoot)
            || relativeToRoot.Equals("..", StringComparison.Ordinal)
            || relativeToRoot.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException("结果包内包含非法文件路径。");
        }

        return targetFullPath;
    }

    public static async Task<string> ExtractAndValidateAsync(
        ZipArchive archive,
        MeasurementArchiveFile file,
        string targetRoot,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(file);

        var entryName = NormalizeEntryName(file.EntryName);
        var entry = archive.GetEntry(entryName)
            ?? throw new InvalidDataException($"结果包缺少文件：{entryName}");
        if (entry.Length != file.Size)
        {
            throw new InvalidDataException($"结果包文件大小校验失败：{entryName}");
        }

        var targetPath = ResolveSafePath(targetRoot, file.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        try
        {
            await using (var source = entry.Open())
            await using (var target = new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            await using var validationStream = new FileStream(
                targetPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            var actualHash = Convert.ToHexString(
                await SHA256.HashDataAsync(validationStream, cancellationToken));
            if (!actualHash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"结果包文件完整性校验失败：{entryName}");
            }

            return targetPath;
        }
        catch
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            throw;
        }
    }

    private static string NormalizeEntryName(string entryName)
    {
        return entryName.Replace('\\', '/').TrimStart('/');
    }
}
