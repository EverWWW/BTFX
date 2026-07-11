using System.IO;

namespace BTFX.Data;

public sealed class DatabaseRecoveryManager
{
    public string CaptureExistingDatabase(string databasePath)
    {
        return Capture(databasePath, GetRecoveryRoot(databasePath));
    }

    public string CreateInitializationGuard(string databasePath)
    {
        var dataDirectory = GetDataDirectory(databasePath);
        return Capture(databasePath, Path.Combine(dataDirectory, "Temp", "DatabaseInitGuard"));
    }

    public string PromoteGuardToRecovery(string databasePath, string guardDirectory)
    {
        var recoveryDirectory = Path.Combine(
            GetRecoveryRoot(databasePath),
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        Directory.CreateDirectory(Path.GetDirectoryName(recoveryDirectory)!);
        Directory.Move(guardDirectory, recoveryDirectory);
        return recoveryDirectory;
    }

    public void DiscardGuard(string? guardDirectory)
    {
        if (!string.IsNullOrWhiteSpace(guardDirectory) && Directory.Exists(guardDirectory))
        {
            Directory.Delete(guardDirectory, recursive: true);
        }
    }

    private static string Capture(string databasePath, string targetRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var recoveryDirectory = Path.Combine(targetRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(recoveryDirectory);

        foreach (var sourcePath in GetDatabaseFiles(databasePath))
        {
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, Path.Combine(recoveryDirectory, Path.GetFileName(sourcePath)), overwrite: false);
            }
        }

        return recoveryDirectory;
    }

    private static string GetRecoveryRoot(string databasePath) =>
        Path.Combine(GetDataDirectory(databasePath), "Recovery");

    private static string GetDataDirectory(string databasePath) =>
        Directory.GetParent(Path.GetDirectoryName(databasePath)!)?.FullName
        ?? Path.GetDirectoryName(databasePath)!;

    internal static IEnumerable<string> GetDatabaseFiles(string databasePath)
    {
        yield return databasePath;
        yield return databasePath + "-wal";
        yield return databasePath + "-shm";
        yield return databasePath + "-journal";
    }
}
