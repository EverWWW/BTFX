using BTFX.Views.Measurement;
using Xunit;

namespace BTFX.Tests;

public sealed class ReviewPreviewEncodingTests
{
    [Fact]
    public void BuildCombinedPreviewFilter_UsesConfiguredPreviewHeight()
    {
        var filter = Step3ReviewView.BuildCombinedPreviewFilter(540, 24);

        Assert.Contains("scale=-2:540", filter, StringComparison.Ordinal);
        Assert.Contains("fps=24", filter, StringComparison.Ordinal);
        Assert.Contains("hstack=inputs=2", filter, StringComparison.Ordinal);
    }
}
