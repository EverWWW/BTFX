using System.IO;

namespace BTFX.Services.Implementations;

internal sealed class MeasurementResultFileQuarantine : IDisposable
{
    private readonly List<(string OriginalPath, string QuarantinePath)> _moves = [];
    private readonly string _sessionDirectory;
    private readonly List<string> _ownedDirectories;
    private bool _committed;
    private bool _disposed;

    public MeasurementResultFileQuarantine(
        IEnumerable<string?> candidateDirectories,
        IEnumerable<string> managedRoots,
        string quarantineRoot)
    {
        var roots = managedRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _ownedDirectories = CollapseNestedDirectories(candidateDirectories
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.IsPathRooted(path!)
                ? Path.GetFullPath(path!)
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path!)))
            .Where(Directory.Exists)
            .Where(path => roots.Any(root => IsOwnedChild(path, root))));
        _sessionDirectory = Path.Combine(
            Path.GetFullPath(quarantineRoot),
            Guid.NewGuid().ToString("N"));
    }

    public void Stage()
    {
        if (_moves.Count > 0 || _ownedDirectories.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(_sessionDirectory);
        try
        {
            for (var index = 0; index < _ownedDirectories.Count; index++)
            {
                var originalPath = _ownedDirectories[index];
                var quarantinePath = Path.Combine(_sessionDirectory, $"item_{index:D4}");
                Directory.Move(originalPath, quarantinePath);
                _moves.Add((originalPath, quarantinePath));
            }
        }
        catch
        {
            Restore();
            throw;
        }
    }

    public void Restore()
    {
        if (_committed)
        {
            return;
        }

        for (var index = _moves.Count - 1; index >= 0; index--)
        {
            var move = _moves[index];
            if (!Directory.Exists(move.QuarantinePath))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(move.OriginalPath)!);
            Directory.Move(move.QuarantinePath, move.OriginalPath);
        }

        _moves.Clear();
        DeleteSessionDirectoryIfEmpty();
    }

    public void CommitDelete()
    {
        if (_committed)
        {
            return;
        }

        _committed = true;
        if (Directory.Exists(_sessionDirectory))
        {
            Directory.Delete(_sessionDirectory, recursive: true);
        }

        _moves.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (!_committed)
        {
            Restore();
        }

        _disposed = true;
    }

    private static bool IsOwnedChild(string candidate, string root)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return !relative.Equals(".", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative)
            && !relative.Equals("..", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static List<string> CollapseNestedDirectories(IEnumerable<string> directories)
    {
        var result = new List<string>();
        foreach (var directory in directories
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(path => path.Length))
        {
            if (result.Any(parent => IsOwnedChild(directory, parent)))
            {
                continue;
            }

            result.Add(directory);
        }

        return result;
    }

    private void DeleteSessionDirectoryIfEmpty()
    {
        if (Directory.Exists(_sessionDirectory)
            && !Directory.EnumerateFileSystemEntries(_sessionDirectory).Any())
        {
            Directory.Delete(_sessionDirectory);
        }
    }
}
