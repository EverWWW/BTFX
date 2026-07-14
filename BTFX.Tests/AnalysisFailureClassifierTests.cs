using BTFX.Services.Implementations;
using Xunit;

namespace BTFX.Tests;

public sealed class AnalysisFailureClassifierTests
{
    [Theory]
    [InlineData("list indices must be integers or slices, not tuple")]
    [InlineData("Analysis failed, stage: pose_estimation_side, reason: list indices must be integers or slices, not tuple")]
    [InlineData("At least one of the following markers is missing for computing the height of the person.")]
    public void Classify_ReturnsMissingBodyKeypoints_ForInvalidPoseData(string message)
    {
        Assert.Equal(AnalysisFailureKind.MissingBodyKeypoints, AnalysisFailureClassifier.Classify(message));
    }

    [Fact]
    public void Classify_ReturnsInputVideoUnavailable_WhenAlgorithmCannotOpenVideo()
    {
        const string message = "Could not open C:\\video\\side.mp4. Check that the file exists.";

        Assert.Equal(AnalysisFailureKind.InputVideoUnavailable, AnalysisFailureClassifier.Classify(message));
    }

    [Theory]
    [InlineData("Body keypoint detection exceeded 5 minutes and was stopped.")]
    [InlineData("人体关键点识别超过 5 分钟，任务已自动停止。")]
    public void Classify_ReturnsTimeout_ForApplicationTimeoutMessages(string message)
    {
        Assert.Equal(AnalysisFailureKind.Timeout, AnalysisFailureClassifier.Classify(message));
    }

    [Fact]
    public void Classify_ReturnsUnknown_WithoutExposingUnrecognizedAlgorithmDetails()
    {
        Assert.Equal(AnalysisFailureKind.Unknown, AnalysisFailureClassifier.Classify("unexpected python traceback"));
    }
}
