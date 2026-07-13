namespace BTFX.Services.Implementations;

internal static class AnalysisTimeoutPolicy
{
    internal const int MaximumTotalTimeoutMinutes = 10;

    public static int GetEffectiveTotalTimeoutMinutes(int configuredMinutes)
        => Math.Clamp(configuredMinutes, 1, MaximumTotalTimeoutMinutes);
}

internal enum AlgorithmWatchdogTimeoutKind
{
    Startup,
    PoseEstimation,
    Processing
}

internal sealed record AlgorithmWatchdogTimeout(
    AlgorithmWatchdogTimeoutKind Kind,
    string? Stage,
    TimeSpan Limit);

internal sealed class AlgorithmProgressWatchdog
{
    private readonly object _sync = new();
    private readonly DateTimeOffset _startedAt;
    private readonly TimeSpan _startupTimeout;
    private readonly TimeSpan _poseTimeout;
    private readonly TimeSpan _processingTimeout;
    private string? _currentStage;
    private DateTimeOffset? _stageStartedAt;

    public AlgorithmProgressWatchdog(
        DateTimeOffset startedAt,
        TimeSpan startupTimeout,
        TimeSpan poseTimeout,
        TimeSpan processingTimeout)
    {
        _startedAt = startedAt;
        _startupTimeout = startupTimeout;
        _poseTimeout = poseTimeout;
        _processingTimeout = processingTimeout;
    }

    public void ObserveStage(string? stage, DateTimeOffset observedAt)
    {
        var normalizedStage = string.IsNullOrWhiteSpace(stage)
            ? "processing"
            : stage.Trim().ToLowerInvariant();

        lock (_sync)
        {
            if (string.Equals(_currentStage, normalizedStage, StringComparison.Ordinal))
            {
                return;
            }

            _currentStage = normalizedStage;
            _stageStartedAt = observedAt;
        }
    }

    public AlgorithmWatchdogTimeout? Check(DateTimeOffset now)
    {
        lock (_sync)
        {
            if (_stageStartedAt is null)
            {
                return now - _startedAt >= _startupTimeout
                    ? new AlgorithmWatchdogTimeout(AlgorithmWatchdogTimeoutKind.Startup, null, _startupTimeout)
                    : null;
            }

            if (IsTerminalStage(_currentStage))
            {
                return null;
            }

            var isPoseStage = _currentStage?.StartsWith("pose_estimation_", StringComparison.Ordinal) == true
                && !_currentStage.EndsWith("_ok", StringComparison.Ordinal);
            var limit = isPoseStage ? _poseTimeout : _processingTimeout;
            if (now - _stageStartedAt.Value < limit)
            {
                return null;
            }

            return new AlgorithmWatchdogTimeout(
                isPoseStage ? AlgorithmWatchdogTimeoutKind.PoseEstimation : AlgorithmWatchdogTimeoutKind.Processing,
                _currentStage,
                limit);
        }
    }

    private static bool IsTerminalStage(string? stage)
        => string.Equals(stage, "completed", StringComparison.Ordinal)
           || string.Equals(stage, "failed", StringComparison.Ordinal);
}

internal sealed class AlgorithmStageTimeoutException : TimeoutException
{
    public AlgorithmStageTimeoutException(AlgorithmWatchdogTimeout timeout)
        : base($"Algorithm stage timed out: {timeout.Stage ?? "startup"}")
    {
        Timeout = timeout;
    }

    public AlgorithmWatchdogTimeout Timeout { get; }
}
