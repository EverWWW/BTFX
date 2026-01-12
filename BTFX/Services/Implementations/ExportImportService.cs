using System.IO;
using System.Text;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 导出导入服务实现
/// </summary>
public class ExportImportService : IExportImportService
{
    private readonly ILogHelper? _logHelper;

    public ExportImportService()
    {
        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }
    }

    /// <inheritdoc/>
    public async Task<bool> ExportPatientsAsync(List<Patient> patients, ExportFormat format, string filePath)
    {
        try
        {
            _logHelper?.Information($"开始导出患者数据：{patients.Count}条，格式：{format}");

            var exportData = patients.Select(p => new PatientExportModel
            {
                姓名 = p.Name,
                性别 = p.Gender == Gender.Male ? "男" : "女",
                年龄 = p.Age?.ToString() ?? "",
                电话 = p.Phone,
                证件号 = p.IdNumber ?? "",
                身高cm = p.Height?.ToString("F1") ?? "",
                体重kg = p.Weight?.ToString("F1") ?? "",
                创建时间 = p.CreatedAt.ToString(Constants.DATETIME_FORMAT)
            }).ToList();

            return format switch
            {
                ExportFormat.Excel => await ExportToExcelAsync(exportData, filePath),
                ExportFormat.CSV => await ExportToCsvAsync(exportData, filePath),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logHelper?.Error("导出患者数据失败", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ExportMeasurementsAsync(List<MeasurementRecord> measurements, ExportFormat format, string filePath)
    {
        try
        {
            _logHelper?.Information($"开始导出测量数据：{measurements.Count}条，格式：{format}");

            var exportData = measurements.Select(m => new MeasurementExportModel
            {
                患者姓名 = m.Patient?.Name ?? "",
                性别 = m.Patient?.Gender == Gender.Male ? "男" : "女",
                年龄 = m.Patient?.Age?.ToString() ?? "",
                测量日期 = m.MeasurementDate.ToString(Constants.DATETIME_FORMAT),
                测量状态 = GetStatusText(m.Status),
                测量时长秒 = m.DurationSeconds?.ToString() ?? "",
                操作员 = m.Operator?.Name ?? "",
                左脚步幅cm = m.GaitParameters?.StrideLengthLeft?.ToString("F2") ?? "",
                右脚步幅cm = m.GaitParameters?.StrideLengthRight?.ToString("F2") ?? "",
                步频步每分 = m.GaitParameters?.Cadence?.ToString("F1") ?? "",
                步速m每s = m.GaitParameters?.Velocity?.ToString("F2") ?? "",
                左脚支撑相百分比 = m.GaitParameters?.StancePhaseLeft?.ToString("F1") ?? "",
                右脚支撑相百分比 = m.GaitParameters?.StancePhaseRight?.ToString("F1") ?? "",
                双支撑时间百分比 = m.GaitParameters?.DoubleSupport?.ToString("F1") ?? "",
                备注 = m.Remark ?? ""
            }).ToList();

            return format switch
            {
                ExportFormat.Excel => await ExportToExcelAsync(exportData, filePath),
                ExportFormat.CSV => await ExportToCsvAsync(exportData, filePath),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logHelper?.Error("导出测量数据失败", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<List<Patient>> ImportPatientsAsync(string filePath)
    {
        var patients = new List<Patient>();

        try
        {
            if (!File.Exists(filePath))
            {
                _logHelper?.Error($"导入文件不存在: {filePath}");
                return patients;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            _logHelper?.Information($"开始导入患者数据: {filePath}");

            if (extension == ".csv")
            {
                patients = await ImportPatientsFromCsvAsync(filePath);
            }
            else if (extension == ".xlsx" || extension == ".xls")
            {
                // Excel 导入需要第三方库，暂时提示不支持
                _logHelper?.Warning("暂不支持 Excel 格式导入，请使用 CSV 格式");
            }
            else
            {
                _logHelper?.Error($"不支持的文件格式: {extension}");
            }

            _logHelper?.Information($"导入完成，成功导入 {patients.Count} 条患者数据");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("导入患者数据失败", ex);
        }

        return patients;
    }

    /// <summary>
    /// 从 CSV 导入患者数据
    /// </summary>
    private async Task<List<Patient>> ImportPatientsFromCsvAsync(string filePath)
    {
        var patients = new List<Patient>();

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);
            if (lines.Length < 2)
            {
                _logHelper?.Warning("CSV 文件为空或只有表头");
                return patients;
            }

            // 解析表头
            var headers = ParseCsvLine(lines[0]);
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
            {
                headerMap[headers[i].Trim()] = i;
            }

            // 解析数据行
            for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var values = ParseCsvLine(line);
                    var patient = new Patient();

                    // 姓名（必填）
                    if (headerMap.TryGetValue("姓名", out var nameIndex) && nameIndex < values.Length)
                    {
                        patient.Name = values[nameIndex].Trim();
                    }
                    if (string.IsNullOrEmpty(patient.Name))
                    {
                        _logHelper?.Warning($"第 {lineIndex + 1} 行缺少姓名，跳过");
                        continue;
                    }

                    // 性别
                    if (headerMap.TryGetValue("性别", out var genderIndex) && genderIndex < values.Length)
                    {
                        var genderText = values[genderIndex].Trim();
                        patient.Gender = genderText == "女" ? Gender.Female : Gender.Male;
                    }

                    // 电话（必填）
                    if (headerMap.TryGetValue("电话", out var phoneIndex) && phoneIndex < values.Length)
                    {
                        patient.Phone = values[phoneIndex].Trim();
                    }
                    if (string.IsNullOrEmpty(patient.Phone))
                    {
                        _logHelper?.Warning($"第 {lineIndex + 1} 行缺少电话，跳过");
                        continue;
                    }

                    // 证件号
                    if (headerMap.TryGetValue("证件号", out var idIndex) && idIndex < values.Length)
                    {
                        patient.IdNumber = values[idIndex].Trim();
                    }

                    // 身高
                    if (headerMap.TryGetValue("身高cm", out var heightIndex) && heightIndex < values.Length)
                    {
                        if (double.TryParse(values[heightIndex].Trim(), out var height))
                        {
                            patient.Height = height;
                        }
                    }

                    // 体重
                    if (headerMap.TryGetValue("体重kg", out var weightIndex) && weightIndex < values.Length)
                    {
                        if (double.TryParse(values[weightIndex].Trim(), out var weight))
                        {
                            patient.Weight = weight;
                        }
                    }

                    // 设置默认值
                    patient.Status = PatientStatus.Active;
                    patient.CreatedAt = DateTime.Now;
                    patient.UpdatedAt = DateTime.Now;

                    patients.Add(patient);
                }
                catch (Exception ex)
                {
                    _logHelper?.Warning($"解析第 {lineIndex + 1} 行失败: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("解析 CSV 文件失败", ex);
        }

        return patients;
    }

    /// <summary>
    /// 解析 CSV 行
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // 检查是否是转义的引号
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // 跳过下一个引号
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        result.Add(current.ToString());
        return result.ToArray();
    }

    /// <inheritdoc/>
    public Task<bool> ExportReportToExcelAsync(int reportId, string filePath)
    {
        // TODO: 导出报告为Excel（第四阶段完善）
        _logHelper?.Information($"导出报告为Excel：ID={reportId}, 文件={filePath}");
        return Task.FromResult(false);
    }

    #region 私有方法

    /// <summary>
    /// 导出到Excel（使用HTML table格式，Excel可以正确打开）
    /// </summary>
    private async Task<bool> ExportToExcelAsync<T>(List<T> data, string filePath) where T : class, new()
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var properties = typeof(T).GetProperties();
            var sb = new StringBuilder();

            // HTML 表格格式（Excel 可以正确识别）
            sb.AppendLine("<?xml version=\"1.0\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
            sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
            sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\"");
            sb.AppendLine(" xmlns:html=\"http://www.w3.org/TR/REC-html40\">");
            sb.AppendLine("<Worksheet ss:Name=\"数据\">");
            sb.AppendLine("<Table>");

            // 写入表头
            sb.AppendLine("<Row>");
            foreach (var prop in properties)
            {
                sb.AppendLine($"<Cell><Data ss:Type=\"String\">{EscapeXml(prop.Name)}</Data></Cell>");
            }
            sb.AppendLine("</Row>");

            // 写入数据
            foreach (var item in data)
            {
                sb.AppendLine("<Row>");
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(item)?.ToString() ?? "";
                    sb.AppendLine($"<Cell><Data ss:Type=\"String\">{EscapeXml(value)}</Data></Cell>");
                }
                sb.AppendLine("</Row>");
            }

            sb.AppendLine("</Table>");
            sb.AppendLine("</Worksheet>");
            sb.AppendLine("</Workbook>");

            // 写入文件（使用UTF-8 with BOM）
            await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(true));

            _logHelper?.Information($"Excel导出成功：{filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"Excel导出失败：{filePath}", ex);
            return false;
        }
    }

    /// <summary>
    /// 转义XML特殊字符
    /// </summary>
    private static string EscapeXml(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    /// <summary>
    /// 导出到CSV
    /// </summary>
    private async Task<bool> ExportToCsvAsync<T>(List<T> data, string filePath) where T : class, new()
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var properties = typeof(T).GetProperties();
            var sb = new StringBuilder();

            // 写入表头
            sb.AppendLine(string.Join(",", properties.Select(p => EscapeCsvField(p.Name))));

            // 写入数据
            foreach (var item in data)
            {
                var values = properties.Select(p => EscapeCsvField(p.GetValue(item)?.ToString() ?? ""));
                sb.AppendLine(string.Join(",", values));
            }

            // 写入文件（使用UTF-8 with BOM）
            await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(true));

            _logHelper?.Information($"CSV导出成功：{filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"CSV导出失败：{filePath}", ex);
            return false;
        }
    }

    /// <summary>
    /// 转义CSV字段
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";

        // 如果包含逗号、引号或换行符，需要用引号包围
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    /// <summary>
    /// 获取状态文本
    /// </summary>
    private static string GetStatusText(MeasurementStatus status)
    {
        return status switch
        {
            MeasurementStatus.Pending => "待处理",
            MeasurementStatus.InProgress => "进行中",
            MeasurementStatus.Completed => "已完成",
            MeasurementStatus.Cancelled => "已取消",
            MeasurementStatus.Failed => "测量失败",
            _ => "未知"
        };
    }

    #endregion
}

#region 导出数据模型

/// <summary>
/// 患者导出模型
/// </summary>
public class PatientExportModel
{
    public string 姓名 { get; set; } = "";
    public string 性别 { get; set; } = "";
    public string 年龄 { get; set; } = "";
    public string 电话 { get; set; } = "";
    public string 证件号 { get; set; } = "";
    public string 身高cm { get; set; } = "";
    public string 体重kg { get; set; } = "";
    public string 创建时间 { get; set; } = "";
}

/// <summary>
/// 测量数据导出模型
/// </summary>
public class MeasurementExportModel
{
    public string 患者姓名 { get; set; } = "";
    public string 性别 { get; set; } = "";
    public string 年龄 { get; set; } = "";
    public string 测量日期 { get; set; } = "";
    public string 测量状态 { get; set; } = "";
    public string 测量时长秒 { get; set; } = "";
    public string 操作员 { get; set; } = "";
    public string 左脚步幅cm { get; set; } = "";
    public string 右脚步幅cm { get; set; } = "";
    public string 步频步每分 { get; set; } = "";
    public string 步速m每s { get; set; } = "";
    public string 左脚支撑相百分比 { get; set; } = "";
    public string 右脚支撑相百分比 { get; set; } = "";
    public string 双支撑时间百分比 { get; set; } = "";
    public string 备注 { get; set; } = "";
}

#endregion
