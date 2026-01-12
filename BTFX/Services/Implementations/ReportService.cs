using BTFX.Common;
using BTFX.Data;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 报告服务实现（使用 SqlSugar）
/// </summary>
public class ReportService : IReportService
{
    private readonly IMeasurementService _measurementService;
    private readonly ILogHelper? _logHelper;

    // 报告序号（每日重置）
    private static int _reportSequence = 1;
    private static DateTime _lastSequenceDate = DateTime.Today;
    private static readonly object _sequenceLock = new();

    public ReportService(IMeasurementService measurementService)
    {
        _measurementService = measurementService;

        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }
    }

    /// <inheritdoc/>
    public async Task<List<Report>> GetReportsByPatientIdAsync(int patientId)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var reports = await db.Queryable<Report>()
                .Where(r => r.PatientId == patientId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // 加载关联数据
            foreach (var report in reports)
            {
                report.MeasurementRecord = await _measurementService.GetMeasurementByIdAsync(report.MeasurementId);
                report.Patient = report.MeasurementRecord?.Patient;
            }

            return reports;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取患者报告列表失败: PatientId={patientId}", ex);
            return new List<Report>();
        }
    }

    /// <inheritdoc/>
    public async Task<Report?> GetReportByMeasurementIdAsync(int measurementRecordId)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var report = await db.GetFirstAsync<Report>(r => r.MeasurementId == measurementRecordId);

            if (report != null)
            {
                report.MeasurementRecord = await _measurementService.GetMeasurementByIdAsync(report.MeasurementId);
                report.Patient = report.MeasurementRecord?.Patient;
            }

            return report;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取测量报告失败: MeasurementId={measurementRecordId}", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<Report?> GetReportByIdAsync(int id)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var report = await db.GetByIdAsync<Report>(id);

            if (report != null)
            {
                report.MeasurementRecord = await _measurementService.GetMeasurementByIdAsync(report.MeasurementId);
                report.Patient = report.MeasurementRecord?.Patient;
            }

            return report;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取报告失败: Id={id}", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<int> CreateReportAsync(Report report)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var now = DateTime.Now;
            report.ReportDate = now;
            report.CreatedAt = now;
            report.UpdatedAt = now;

            var id = await db.InsertReturnIdentityAsync(report);

            _logHelper?.Information($"创建报告成功: Id={id}, Number={report.ReportNumber}");
            return (int)id;
        }
        catch (Exception ex)
        {
            _logHelper?.Error("创建报告失败", ex);
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateReportAsync(Report report)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            report.UpdatedAt = DateTime.Now;

            var count = await db.UpdateAsync<Report>(
                r => new Report
                {
                    DoctorOpinion = report.DoctorOpinion,
                    Status = report.Status,
                    PdfFilePath = report.PdfFilePath,
                    UpdatedAt = report.UpdatedAt
                },
                r => r.Id == report.Id);

            return count > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"更新报告失败: Id={report.Id}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteReportAsync(int id)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var success = await db.DeleteByIdAsync<Report>(id);

            if (success)
            {
                _logHelper?.Information($"删除报告成功: Id={id}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除报告失败: Id={id}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<string> GeneratePdfAsync(int reportId)
    {
        var report = await GetReportByIdAsync(reportId);
        if (report == null) return string.Empty;

        try
        {
            // 使用 ReportPdfExporter 生成PDF
            var settingsService = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService;
            if (settingsService != null)
            {
                var exporter = new Helpers.ReportPdfExporter(settingsService);

                // 创建PDF保存目录
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var reportDir = System.IO.Path.Combine(baseDir, Common.Constants.REPORT_DIRECTORY);
                if (!System.IO.Directory.Exists(reportDir))
                {
                    System.IO.Directory.CreateDirectory(reportDir);
                }

                var fileName = $"Report_{report.ReportNumber}_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                var filePath = System.IO.Path.Combine(reportDir, fileName);

                if (exporter.ExportToPdf(report, filePath))
                {
                    report.PdfFilePath = filePath;
                    await UpdateReportAsync(report);
                    _logHelper?.Information($"生成报告PDF成功：{filePath}");
                    return filePath;
                }
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"生成报告PDF失败：ReportId={reportId}", ex);
        }

        return string.Empty;
    }

    /// <inheritdoc/>
    public async Task<bool> PrintReportAsync(int reportId)
    {
        var report = await GetReportByIdAsync(reportId);
        if (report == null) return false;

        try
        {
            // 使用 ReportPreviewHelper 生成 FlowDocument
            var settingsService = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService;
            var unitName = settingsService?.CurrentSettings?.Unit?.Name ?? Common.Constants.APP_DISPLAY_NAME;

            var document = Helpers.ReportPreviewHelper.GenerateReportDocument(report, unitName);

            // 使用 PrintHelper 打印
            var success = Helpers.PrintHelper.PrintDocument(document, $"报告_{report.ReportNumber}", true);

            if (success)
            {
                report.Status = ReportStatus.Printed;
                report.PrintedAt = DateTime.Now;
                await UpdateReportAsync(report);
                _logHelper?.Information($"打印报告成功：ReportId={reportId}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"打印报告失败：ReportId={reportId}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateReportStatusAsync(int reportId, ReportStatus status)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var now = DateTime.Now;

            var count = await db.UpdateAsync<Report>(
                r => new Report
                {
                    Status = status,
                    UpdatedAt = now
                },
                r => r.Id == reportId);

            return count > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"更新报告状态失败: Id={reportId}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public string GenerateReportNumber()
    {
        lock (_sequenceLock)
        {
            // 检查是否需要重置序号（新的一天）
            if (DateTime.Today > _lastSequenceDate)
            {
                _reportSequence = 1;
                _lastSequenceDate = DateTime.Today;
            }

            // 格式：RPT-YYYYMMDD-XXXX
            var number = $"RPT-{DateTime.Now:yyyyMMdd}-{_reportSequence:D4}";
            _reportSequence++;
            return number;
        }
    }

    /// <summary>
    /// 获取报告列表（带筛选）
    /// </summary>
    public async Task<List<Report>> GetReportsAsync(string? patientName, DateTime? startDate, DateTime? endDate)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var query = db.Queryable<Report>();

            // 添加日期筛选
            if (startDate.HasValue)
            {
                query = query.Where(r => r.ReportDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(r => r.ReportDate <= endDate.Value);
            }

            // 如果有患者名称筛选，需要关联查询
            if (!string.IsNullOrWhiteSpace(patientName))
            {
                var patientIds = await db.Queryable<Patient>()
                    .Where(p => p.Name.Contains(patientName))
                    .Select(p => p.Id)
                    .ToListAsync();

                if (patientIds.Any())
                {
                    query = query.Where(r => patientIds.Contains(r.PatientId));
                }
                else
                {
                    return new List<Report>();
                }
            }

            var reports = await query
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // 加载关联数据
            foreach (var report in reports)
            {
                report.MeasurementRecord = await _measurementService.GetMeasurementByIdAsync(report.MeasurementId);
                report.Patient = report.MeasurementRecord?.Patient;
            }

            return reports;
        }
        catch (Exception ex)
        {
            _logHelper?.Error("获取报告列表失败", ex);
            return new List<Report>();
        }
    }

    /// <summary>
    /// 生成报告
    /// </summary>
    public async Task<Report?> GenerateReportAsync(int measurementRecordId, int operatorId)
    {
        // 检查是否已有报告
        var existing = await GetReportByMeasurementIdAsync(measurementRecordId);

        // 获取测量记录
        var measurement = await _measurementService.GetMeasurementByIdAsync(measurementRecordId);
        if (measurement == null)
        {
            _logHelper?.Error($"生成报告失败：找不到测量记录 ID={measurementRecordId}");
            return null;
        }

        if (existing != null)
        {
            // 更新现有报告
            existing.Status = ReportStatus.Draft;
            existing.DoctorOpinion = string.Empty;
            await UpdateReportAsync(existing);
            _logHelper?.Information($"覆盖报告：ID={existing.Id}");
            return existing;
        }
        else
        {
            // 创建新报告
            var report = new Report
            {
                ReportNumber = GenerateReportNumber(),
                MeasurementId = measurementRecordId,
                MeasurementRecord = measurement,
                PatientId = measurement.PatientId,
                Patient = measurement.Patient,
                CreatedBy = operatorId,
                Status = ReportStatus.Draft,
                DoctorOpinion = string.Empty,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var id = await CreateReportAsync(report);
            report.Id = id;
            _logHelper?.Information($"创建报告：ID={report.Id}, Number={report.ReportNumber}");
            return report;
        }
    }
}
