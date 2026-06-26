using System.IO;
using System.Text.RegularExpressions;
using BTFX.Common;

namespace BTFX.Helpers;

public static partial class AlgorithmExecutableResolver
{
    private const string LegacyGpuAlgorithmExeFileName = "gait_analysis_gpu.exe";

    public static string Resolve(string? configuredPath)
    {
        var path = NormalizeConfiguredPath(configuredPath);
        var resolvedPath = Path.IsPathRooted(path)
            ? path
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

        if (Directory.Exists(resolvedPath))
        {
            return FindBestExecutable(resolvedPath) ?? resolvedPath;
        }

        if (File.Exists(resolvedPath))
        {
            return resolvedPath;
        }

        var configuredDirectory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(configuredDirectory) && Directory.Exists(configuredDirectory))
        {
            var executable = FindBestExecutable(configuredDirectory);
            if (!string.IsNullOrWhiteSpace(executable))
            {
                return executable;
            }
        }

        var defaultDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.ALGORITHM_DIRECTORY);
        return Directory.Exists(defaultDirectory)
            ? FindBestExecutable(defaultDirectory) ?? resolvedPath
            : resolvedPath;
    }

    public static string ToConfigPath(string resolvedPath)
    {
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return Constants.ALGORITHM_DIRECTORY;
        }

        return Path.IsPathRooted(resolvedPath)
            ? Path.GetRelativePath(AppDomain.CurrentDomain.BaseDirectory, resolvedPath)
            : resolvedPath;
    }

    private static string NormalizeConfiguredPath(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath) || IsLegacyAlgorithmPath(configuredPath))
        {
            return Constants.ALGORITHM_DIRECTORY;
        }

        return configuredPath;
    }

    private static bool IsLegacyAlgorithmPath(string configuredPath)
    {
        var normalized = configuredPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);
        if (!string.Equals(fileName, "Gait_analysis.exe", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fileName, "gait_analysis.exe", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fileName, LegacyGpuAlgorithmExeFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var directoryName = Path.GetFileName(Path.GetDirectoryName(normalized)?.TrimEnd(Path.DirectorySeparatorChar));
        return string.Equals(directoryName, "Algorithm", StringComparison.OrdinalIgnoreCase)
            || string.Equals(directoryName, "gait_analysis", StringComparison.OrdinalIgnoreCase)
            || string.Equals(directoryName, Constants.ALGORITHM_DIRECTORY, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindBestExecutable(string directory)
    {
        return Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
            .OrderByDescending(IsPreferredGpuExecutable)
            .ThenByDescending(GetVersionScore)
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool IsPreferredGpuExecutable(string path)
    {
        return Path.GetFileNameWithoutExtension(path)
            .StartsWith("gait_analysis_gpu", StringComparison.OrdinalIgnoreCase);
    }

    private static Version GetVersionScore(string path)
    {
        var match = VersionPattern().Match(Path.GetFileNameWithoutExtension(path));
        return match.Success && Version.TryParse(match.Value, out var version)
            ? version
            : new Version(0, 0);
    }

    [GeneratedRegex(@"\d+(?:\.\d+)+")]
    private static partial Regex VersionPattern();
}
