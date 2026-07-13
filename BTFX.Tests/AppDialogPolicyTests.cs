using Xunit;

namespace BTFX.Tests;

public sealed class AppDialogPolicyTests
{
    [Fact]
    public void MainApplication_DoesNotUseSystemMessageBox()
    {
        var projectDirectory = FindProjectDirectory();
        var violations = Directory
            .EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { Path = path, Line = line, Number = index + 1 }))
            .Where(item => item.Line.Contains("MessageBox.Show(", StringComparison.Ordinal))
            .Select(item => $"{Path.GetRelativePath(projectDirectory, item.Path)}:{item.Number}")
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "The main application must use AppDialog instead of MessageBox.Show:\n" +
            string.Join(Environment.NewLine, violations));
    }

    private static bool IsGeneratedPath(string path)
    {
        var separator = Path.DirectorySeparatorChar;
        return path.Contains($"{separator}bin{separator}", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"{separator}obj{separator}", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "BTFX", "BTFX.csproj");
            if (File.Exists(candidate))
            {
                return Path.GetDirectoryName(candidate)!;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the BTFX project directory.");
    }
}
