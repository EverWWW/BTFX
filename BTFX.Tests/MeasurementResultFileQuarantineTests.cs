using BTFX.Services.Implementations;
using Xunit;

namespace BTFX.Tests;

public sealed class MeasurementResultFileQuarantineTests
{
    [Fact]
    public void Restore_ReturnsStagedDirectoryToOriginalLocation()
    {
        var testRoot = CreateTestRoot();
        var managedRoot = Path.Combine(testRoot, "Analysis");
        var resultDirectory = Path.Combine(managedRoot, "result_1");
        var quarantineRoot = Path.Combine(testRoot, "Quarantine");
        Directory.CreateDirectory(resultDirectory);
        File.WriteAllText(Path.Combine(resultDirectory, "result.json"), "result");

        try
        {
            using var quarantine = new MeasurementResultFileQuarantine(
                [resultDirectory],
                [managedRoot],
                quarantineRoot);

            quarantine.Stage();
            Assert.False(Directory.Exists(resultDirectory));

            quarantine.Restore();
            Assert.True(File.Exists(Path.Combine(resultDirectory, "result.json")));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void CommitDelete_RemovesOwnedDirectoryButNeverTouchesExternalDirectory()
    {
        var testRoot = CreateTestRoot();
        var managedRoot = Path.Combine(testRoot, "Analysis");
        var resultDirectory = Path.Combine(managedRoot, "result_1");
        var externalDirectory = Path.Combine(testRoot, "ExternalVideo");
        var quarantineRoot = Path.Combine(testRoot, "Quarantine");
        Directory.CreateDirectory(resultDirectory);
        Directory.CreateDirectory(externalDirectory);

        try
        {
            using var quarantine = new MeasurementResultFileQuarantine(
                [resultDirectory, externalDirectory],
                [managedRoot],
                quarantineRoot);

            quarantine.Stage();
            quarantine.CommitDelete();

            Assert.False(Directory.Exists(resultDirectory));
            Assert.True(Directory.Exists(externalDirectory));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static string CreateTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "btfx-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
