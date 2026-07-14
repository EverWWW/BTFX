using System.Text.RegularExpressions;

namespace BTFX.Services.Implementations;

internal enum AnalysisFailureKind
{
    Unknown,
    MissingBodyKeypoints,
    InputVideoUnavailable,
    Timeout
}

internal static class AnalysisFailureClassifier
{
    internal static AnalysisFailureKind Classify(string? rawMessage)
    {
        var message = NormalizeWhitespace(rawMessage).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(message))
        {
            return AnalysisFailureKind.Unknown;
        }

        if (message.Contains("list indices must be integers or slices, not tuple", StringComparison.Ordinal)
            || message.Contains("未检测到完整人体关键点", StringComparison.Ordinal)
            || message.Contains("at least one of the following markers is missing", StringComparison.Ordinal)
            || message.Contains("marker is missing", StringComparison.Ordinal)
            || message.Contains("markers is missing", StringComparison.Ordinal)
            || message.Contains("person is entirely visible", StringComparison.Ordinal)
            || message.Contains("person is entirely wisible", StringComparison.Ordinal))
        {
            return AnalysisFailureKind.MissingBodyKeypoints;
        }

        if (message.Contains("could not open", StringComparison.Ordinal)
            && message.Contains("check that the file exists", StringComparison.Ordinal))
        {
            return AnalysisFailureKind.InputVideoUnavailable;
        }

        if (message.Contains("timed out", StringComparison.Ordinal)
            || message.Contains("made no progress", StringComparison.Ordinal)
            || message.Contains("超时", StringComparison.Ordinal)
            || (message.Contains("exceeded", StringComparison.Ordinal)
                && (message.Contains("minute", StringComparison.Ordinal)
                    || message.Contains("分钟", StringComparison.Ordinal)))
            || (message.Contains("超过", StringComparison.Ordinal)
                && message.Contains("分钟", StringComparison.Ordinal)))
        {
            return AnalysisFailureKind.Timeout;
        }

        return AnalysisFailureKind.Unknown;
    }

    private static string NormalizeWhitespace(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim(), @"\s+", " ");
    }
}
