using System.Text.Json.Serialization;

namespace BTFX.Models.Analysis;

/// <summary>
/// 算法 stdout 实时状态消息（单行 JSON）
/// </summary>
public class TaskStatusMessage
{
    [JsonPropertyName("task_id")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("task_status")]
    public string TaskStatus { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("progress_percent")]
    public int ProgressPercent { get; set; }

    [JsonPropertyName("current_stage")]
    public string? CurrentStage { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error_code")]
    public int? ErrorCode { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("timestamp")]
    public string? TimestampText { get; set; }

    public string EffectiveRequestId => !string.IsNullOrWhiteSpace(RequestId) ? RequestId : TaskId;

    public string EffectiveStatus => !string.IsNullOrWhiteSpace(TaskStatus) ? TaskStatus : Status;

    public int EffectiveProgress => ProgressPercent > 0 ? ProgressPercent : Progress;

    public bool IsStatusMessage =>
        string.Equals(Type, BTFX.Common.Constants.STATUS_MESSAGE_TYPE, StringComparison.OrdinalIgnoreCase) ||
        !string.IsNullOrWhiteSpace(TaskId) ||
        !string.IsNullOrWhiteSpace(Status) ||
        ProgressPercent > 0 ||
        !string.IsNullOrWhiteSpace(CurrentStage);
}
