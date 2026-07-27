using System.Xml.Linq;
using Xunit;

namespace BTFX.Tests;

public sealed class MeasurementLayoutRegressionTests
{
    [Theory]
    [InlineData("MeasurementName")]
    [InlineData("Remark")]
    public void CreateMeasurement_TextInputsAreConstrainedByTheirGridColumns(string bindingName)
    {
        var document = XDocument.Load(FindProjectFile("Views", "Measurement", "Step1CreateMeasurementView.xaml"));
        var input = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "TextBox"
                && element.Attribute("Text")?.Value.Contains(bindingName, StringComparison.Ordinal) == true);

        Assert.Equal("Grid", input.Parent?.Name.LocalName);
        Assert.Equal("Stretch", input.Attribute("HorizontalAlignment")?.Value);
    }

    [Fact]
    public void AnalysisDetail_FirstRowCardsProvideEnoughHeightForDescenders()
    {
        var document = XDocument.Load(FindProjectFile("Views", "Dialogs", "MeasurementDetailDialog.xaml"));
        var cadence = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "TextBlock"
                && element.Attribute("Text")?.Value.Contains("CadenceDisplay", StringComparison.Ordinal) == true
                && element.Attribute("LineHeight") is not null);
        var card = cadence
            .Ancestors()
            .First(element => element.Name.LocalName == "Border" && element.Attribute("Height") is not null);

        Assert.True(double.Parse(card.Attribute("Height")!.Value) >= 330);
        Assert.Equal("20", cadence.Attribute("LineHeight")?.Value);
    }

    private static string FindProjectFile(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName, "BTFX" }
                    .Concat(relativeSegments)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Unable to locate BTFX project file.");
    }
}
