using System.IO.Compression;
using System.Security.Cryptography;
using BTFX.Services.Implementations;
using Xunit;

namespace BTFX.Tests;

public sealed class ArchiveImportFileStagerTests
{
    [Fact]
    public void ResolveSafePath_RejectsSiblingWithCommonPrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "btfx-stage", "analysis_1");

        Assert.Throws<InvalidDataException>(() =>
            ArchiveImportFileStager.ResolveSafePath(root, @"..\analysis_10\result.json"));
    }

    [Fact]
    public async Task ExtractAndValidateAsync_WritesFileWhenSizeAndHashMatch()
    {
        var content = "validated result"u8.ToArray();
        var targetRoot = CreateTempDirectory();
        try
        {
            using var stream = CreateArchive("files/result.json", content);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var file = CreateFileMap("files/result.json", "result.json", content);

            var path = await ArchiveImportFileStager.ExtractAndValidateAsync(
                archive,
                file,
                targetRoot,
                CancellationToken.None);

            Assert.Equal(content, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            Directory.Delete(targetRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractAndValidateAsync_RejectsHashMismatchAndRemovesOutput()
    {
        var content = "tampered result"u8.ToArray();
        var targetRoot = CreateTempDirectory();
        try
        {
            using var stream = CreateArchive("files/result.json", content);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var file = CreateFileMap("files/result.json", "result.json", content);
            file.Sha256 = new string('0', 64);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                ArchiveImportFileStager.ExtractAndValidateAsync(
                    archive,
                    file,
                    targetRoot,
                    CancellationToken.None));

            Assert.False(File.Exists(Path.Combine(targetRoot, "result.json")));
        }
        finally
        {
            Directory.Delete(targetRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractAndValidateAsync_RejectsMissingEntry()
    {
        var targetRoot = CreateTempDirectory();
        try
        {
            using var stream = CreateArchive("files/other.json", "other"u8.ToArray());
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var content = "expected"u8.ToArray();
            var file = CreateFileMap("files/result.json", "result.json", content);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                ArchiveImportFileStager.ExtractAndValidateAsync(
                    archive,
                    file,
                    targetRoot,
                    CancellationToken.None));
        }
        finally
        {
            Directory.Delete(targetRoot, recursive: true);
        }
    }

    private static MeasurementArchiveFile CreateFileMap(string entryName, string relativePath, byte[] content)
    {
        return new MeasurementArchiveFile
        {
            EntryName = entryName,
            RelativePath = relativePath,
            Size = content.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()
        };
    }

    private static MemoryStream CreateArchive(string entryName, byte[] content)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            entryStream.Write(content);
        }

        stream.Position = 0;
        return stream;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "btfx-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
