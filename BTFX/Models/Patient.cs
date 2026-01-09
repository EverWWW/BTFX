using BTFX.Common;

namespace BTFX.Models;

/// <summary>
/// 患者模型
/// </summary>
public class Patient
{
    /// <summary>
    /// 患者ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 姓名
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 性别
    /// </summary>
    public Gender Gender { get; set; } = Gender.Male;

    /// <summary>
    /// 出生日期
    /// </summary>
    public DateTime? BirthDate { get; set; }

    /// <summary>
    /// 电话
    /// </summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// 证件号
    /// </summary>
    public string? IdNumber { get; set; }

    /// <summary>
    /// 身高 (cm)
    /// </summary>
    public double? Height { get; set; }

    /// <summary>
    /// 体重 (kg)
    /// </summary>
    public double? Weight { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// 病史
    /// </summary>
    public string? MedicalHistory { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    public PatientStatus Status { get; set; } = PatientStatus.Active;

    /// <summary>
    /// 创建人ID
    /// </summary>
    public int CreatedBy { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 计算年龄
    /// </summary>
    public int? Age
    {
        get
        {
            if (BirthDate == null) return null;
            var today = DateTime.Today;
            var age = today.Year - BirthDate.Value.Year;
            if (BirthDate.Value.Date > today.AddYears(-age)) age--;
            return age;
        }
    }

    /// <summary>
    /// 性别显示文本
    /// </summary>
    public string GenderDisplay => Gender == Gender.Male ? "男" : "女";
}
