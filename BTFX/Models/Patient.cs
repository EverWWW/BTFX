using BTFX.Common;
using SqlSugar;

namespace BTFX.Models;

/// <summary>
/// 患者模型
/// </summary>
[SugarTable("Patients")]
public class Patient
{
    /// <summary>
    /// 患者ID
    /// </summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    /// <summary>
    /// 姓名
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 性别
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public Gender Gender { get; set; } = Gender.Male;

    /// <summary>
    /// 出生日期
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? BirthDate { get; set; }

    /// <summary>
    /// 电话
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = false)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>
    /// 证件号
    /// </summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? IdNumber { get; set; }

    /// <summary>
    /// 身高 (cm)
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? Height { get; set; }

    /// <summary>
    /// 体重 (kg)
    /// </summary>
    [SugarColumn(IsNullable = true)]
    public double? Weight { get; set; }

    /// <summary>
    /// 地址
    /// </summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Address { get; set; }

    /// <summary>
    /// 病史
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? MedicalHistory { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Remark { get; set; }

    /// <summary>
    /// 状态
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public PatientStatus Status { get; set; } = PatientStatus.Active;

    /// <summary>
    /// 创建人ID
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public int CreatedBy { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    [SugarColumn(IsNullable = false)]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 计算年龄
    /// </summary>
    [SugarColumn(IsIgnore = true)]
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
    [SugarColumn(IsIgnore = true)]
    public string GenderDisplay => Gender == Gender.Male ? "男" : "女";
}
