using System.Xml.Linq;
using Xunit;

namespace BTFX.Tests;

public sealed class CameraCaptureTranscodeLayoutTests
{
    [Fact]
    public void TranscodeStatus_IsCompactAndDoesNotRenderLogLines()
    {
        var projectDirectory = FindProjectDirectory();
        var xamlPath = Path.Combine(projectDirectory, "Views", "Dialogs", "CameraCaptureDialog.xaml");
        var viewModelPath = Path.Combine(projectDirectory, "ViewModels", "CameraCaptureDialogViewModel.cs");
        var xamlText = File.ReadAllText(xamlPath);
        var viewModelText = File.ReadAllText(viewModelPath);

        Assert.DoesNotContain("Text=\"{Binding TranscodeLogText}\"", xamlText, StringComparison.Ordinal);
        Assert.DoesNotContain("IsTranscodingVisible ? L(\"CameraCapture.Status.Transcoding\")", viewModelText, StringComparison.Ordinal);

        var document = XDocument.Load(xamlPath);
        var statusBlocks = document
            .Descendants()
            .Where(element => element.Name.LocalName == "TextBlock")
            .Where(element => (string?)element.Attribute("Text") == "{Binding RecordingStageTextDisplay}")
            .ToArray();

        Assert.Equal(2, statusBlocks.Length);
        Assert.All(statusBlocks, block =>
        {
            Assert.Equal("Wrap", (string?)block.Attribute("TextWrapping"));
            Assert.False(string.IsNullOrWhiteSpace((string?)block.Attribute("MaxWidth")));
        });
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
