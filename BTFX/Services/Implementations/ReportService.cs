using BTFX.Common;
using BTFX.Data;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 报告服务实现
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var reports = await db.QueryAsync<Report>(@"
                SELECT r.Id, r.ReportNumber, r.MeasurementId AS MeasurementRecordId, r.PatientId, 
                       r.UserId AS OperatorId, r.ReportDate, r.DoctorOpinion, r.Status, 
                       r.FilePath AS PdfFilePath, r.CreatedAt, r.UpdatedAt
                FROM Reports r
                WHERE r.PatientId = @PatientId
                ORDER BY r.CreatedAt DESC
            ", new { PatientId = patientId });

            var result = reports.ToList();

            // 加载关联数据
            foreach (var report in result)
            {
                report.MeasurementRecord = await _measurementService.GetMeasurementByIdAsync(report.MeasurementRecordId);
                report.Patient = report.MeasurementRecord?.Patient;
            }

            return result;
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var report = await db.QueryFirstOrDefaultAsync<Report>(@"
                SELECT Id, ReportNumber, MeasurementId AS MeasurementRecordId, PatientId, 
                       UserId AS OperatorId, ReportDate, DoctorOpinion, Status, 
                       FilePath AS PdfFilePath, CreatedAt, UpdatedAt
                FROM Reports
                WHERE MeasurementId = @MeasurementId
            ", new { MeasurementId = measurementRecordId });

            if (report != null)
            {
                report.MeasurementRecord = await _measurementService.GetMeasurementByIdAsync(report.MeasurementRecordId);
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var report = await db.QueryFirstOrDefaultAsync<Report>(@"
                SELECT Id, ReportNumber, MeasurementId AS MeasurementRecordId, PatientId, 
                       UserId AS OperatorId, ReportDate, DoctorOpinion, Status, 
                       FilePath AS PdfFilePath, CreatedAt, UpdatedAt
                FROM Reports
                WHERE Id = @Id
            ", new { Id = id });

            if (report != null)
            {
                report.MeasurementRecord = await _measurementService.GetMeasurementByIdAsync(report.MeasurementRecordId);
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);
            var reportDate = DateTime.Now.ToString(Constants.DATE_FORMAT);

            var id = await db.InsertAndGetIdAsync(@"
                INSERT INTO Reports (ReportNumber, MeasurementId, PatientId, UserId, ReportDate, 
                                    DoctorOpinion, Status, FilePath, CreatedAt, UpdatedAt)
                VALUES (@ReportNumber, @MeasurementId, @PatientId, @UserId, @ReportDate, 
                        @DoctorOpinion, @Status, @FilePath, @CreatedAt, @UpdatedAt)
            ", new
            {
                report.ReportNumber,
                MeasurementId = report.MeasurementRecordId,
                report.PatientId,
                UserId = report.MeasurementRecord?.OperatorId ?? 1,
                ReportDate = reportDate,
                report.DoctorOpinion,
                Status = (int)report.Status,
                FilePath = report.PdfFilePath,
                CreatedAt = now,
                UpdatedAt = now
            });

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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);

            var affected = await db.ExecuteNonQueryAsync(@"
                UPDATE Reports 
                SET DoctorOpinion = @DoctorOpinion, Status = @Status, FilePath = @FilePath, UpdatedAt = @UpdatedAt
                WHERE Id = @Id
            ", new
            {
                report.Id,
                report.DoctorOpinion,
                Status = (int)report.Status,
                FilePath = report.PdfFilePath,
                UpdatedAt = now
            });

            return affected > 0;
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var affected = await db.ExecuteNonQueryAsync("DELETE FROM Reports WHERE Id = @Id", new { Id = id });

            if (affected > 0)
            {
                _logHelper?.Information($"删除报告成功: Id={id}");
            }

            return affected > 0;
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
            // 使用 ReportPdfExporter 导出PDF
            var settingsService = App.Services?.GetService(typeof(ISettingsService)) as ISettingsService;
            if (settingsService != null)
            {
                var exporter = new Helpers.ReportPdfExporter(settingsService);

                // 导出PDF到报告目录
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
            // 使用 ReportPreviewHelper 创建 FlowDocument
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);

            var affected = await db.ExecuteNonQueryAsync(@"
                UPDATE Reports 
                SET Status = @Status, UpdatedAt = @UpdatedAt
                WHERE Id = @Id
            ", new
            {
                Id = reportId,
                Status = (int)status,
                UpdatedAt = now
            });

            return affected > 0;
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            // 构建查询条件
            var whereClause = "WHERE 1=1";
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(patientName))
            {
                whereClause += " AND p.Name LIKE @PatientName";
                parameters["PatientName"] = $"%{patientName}%";
            }

            if (startDate.HasValue)
            {
                whereClause += " AND r.ReportDate >= @StartDate";
                parameters["StartDate"] = startDate.Value.ToString(Constants.DATE_FORMAT);
            }

            if (endDate.HasValue)
            {
                whereClause += " AND r.ReportDate <= @EndDate";
                parameters["EndDate"] = endDate.Value.ToString(Constants.DATE_FORMAT);
            }

            var sql = $@"
                SELECT r.Id, r.ReportNumber, r.MeasurementId AS MeasurementRecordId, r.PatientId, 
                       r.UserId AS OperatorId, r.ReportDate, r.DoctorOpinion, r.Status, 
                       r.FilePath AS PdfFilePath, r.CreatedAt, r.UpdatedAt
                FROM Reports r
                LEFT JOIN Patients p ON r.PatientId = p.Id
                {whereClause}
                ORDER BY r.CreatedAt DESC
            ";

            var reports = await db.QueryAsync<Report>(sql, parameters);
            var result = reports.ToList();

            // 加载关联数据
            foreach (var report in result)
            {
                report.MeasurementRecord = await _measurementService.GetMeasurementByIdAsync(report.MeasurementRecordId);
                report.Patient = report.MeasurementRecord?.Patient;
            }

            return result;
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
            // 覆盖现有报告
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
                MeasurementRecordId = measurementRecordId,
                MeasurementRecord = measurement,
                PatientId = measurement.PatientId,
                Patient = measurement.Patient,
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
