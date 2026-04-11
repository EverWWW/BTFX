using System.Text.Json.Serialization;

namespace BTFX.Models.Analysis;

/// <summary>
/// 算法 stdout 实时状态消息（单行 JSON）
/// </summary>
public class TaskStatusMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("task_status")]
    public string TaskStatus { get; set; } = string.Empty;

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error_code")]
    public int? ErrorCode { get; set; }
}
