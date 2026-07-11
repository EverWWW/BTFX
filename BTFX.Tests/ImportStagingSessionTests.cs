using BTFX.Services.Implementations;
using Xunit;

namespace BTFX.Tests;

public sealed class ImportStagingSessionTests
{
    [Fact]
    public void Rollback_RemovesPromotedDirectory()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "btfx-tests", Guid.NewGuid().ToString("N"));
        var stagingContainer = Path.Combine(testRoot, "staging");
        var finalDirectory = Path.Combine(testRoot, "final");
        Directory.CreateDirectory(Path.Combine(stagingContainer, "payload"));
        File.WriteAllText(Path.Combine(stagingContainer, "payload", "result.json"), "result");

        try
        {
            using var session = new ImportStagingSession(stagingContainer, finalDirectory);

            session.Promote();
            Assert.True(File.Exists(Path.Combine(finalDirectory, "result.json")));

            session.Rollback();
            Assert.False(Directory.Exists(finalDirectory));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void Commit_KeepsPromotedDirectory()
    {
        var testRoot = Path.Combine(Path.GetTempPath(), "btfx-tests", Guid.NewGuid().ToString("N"));
        var stagingContainer = Path.Combine(testRoot, "staging");
        var finalDirectory = Path.Combine(testRoot, "final");
        Directory.CreateDirectory(Path.Combine(stagingContainer, "payload"));

        try
        {
            using (var session = new ImportStagingSession(stagingContainer, finalDirectory))
            {
                session.Promote();
                session.Commit();
            }

            Assert.True(Directory.Exists(finalDirectory));
        }
        finally
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }
    }
}
