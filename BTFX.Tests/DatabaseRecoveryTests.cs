using BTFX.Data;
using Xunit;

namespace BTFX.Tests;

public sealed class DatabaseRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "btfx-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitializeAsync_PreservesExistingDatabaseWhenInitializationFails()
    {
        var databasePath = Path.Combine(_root, "Data", "Database", "btfx.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        var original = new byte[] { 1, 2, 3, 4, 5, 6 };
        await File.WriteAllBytesAsync(databasePath, original);
        await File.WriteAllTextAsync(databasePath + "-wal", "wal-data");

        var initializer = new DatabaseInitializer(databasePath);

        await Assert.ThrowsAnyAsync<Exception>(() => initializer.InitializeAsync());

        Assert.Equal(original, await File.ReadAllBytesAsync(databasePath));

        var recoveryRoot = Path.Combine(_root, "Data", "Recovery");
        var recoveryDirectory = Assert.Single(Directory.GetDirectories(recoveryRoot));
        Assert.Equal(original, await File.ReadAllBytesAsync(Path.Combine(recoveryDirectory, "btfx.db")));
        Assert.Equal("wal-data", await File.ReadAllTextAsync(Path.Combine(recoveryDirectory, "btfx.db-wal")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
