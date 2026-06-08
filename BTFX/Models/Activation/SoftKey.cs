using System.Text.Json.Serialization;

namespace BTFX.Models.Activation;

/// <summary>
/// 软件激活信息。字段名保持与公司通用激活接口一致。
/// </summary>
public class SoftKey
{
    [JsonPropertyName("equipmentNo")]
    public string? EquipmentNo { get; set; }

    [JsonPropertyName("equipmentName")]
    public string? EquipmentName { get; set; }

    [JsonPropertyName("equipmentModel")]
    public string? EquipmentModel { get; set; }

    [JsonPropertyName("equipmentVersion")]
    public string? EquipmentVersion { get; set; }

    [JsonPropertyName("cpuId")]
    public string? CpuId { get; set; }

    [JsonPropertyName("hdId")]
    public string? HdId { get; set; }

    [JsonPropertyName("biosId")]
    public string? BiosId { get; set; }

    [JsonPropertyName("uniqCode")]
    public string? UniqCode { get; set; }

    [JsonPropertyName("equipCode")]
    public string? EquipCode { get; set; }

    [JsonPropertyName("orderSn")]
    public string? OrderSn { get; set; }

    [JsonPropertyName("osVersion")]
    public string? OsVersion { get; set; }

    [JsonPropertyName("macAddress")]
    public string? MacAddress { get; set; }

    [JsonPropertyName("Lkey")]
    public string? LicenseKey { get; set; }

    [JsonPropertyName("Connect")]
    public bool Connect { get; set; }
}
