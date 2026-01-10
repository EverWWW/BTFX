using BTFX.Common;
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

    // 模拟数据存储
    private static readonly List<Report> _mockReports = new();
    private static int _nextId = 1;
    private static int _reportSequence = 1;

    public ReportService(IMeasurementService measurementService)
    {
        _measurementService = measurementService;

        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }

        // 初始化一些模拟数据
        if (_mockReports.Count == 0)
        {
            InitializeMockData();
        }
    }

    /// <summary>
    /// 初始化模拟数据
    /// </summary>
    private void InitializeMockData()
    {
        // 模拟一些报告数据（基于测量数据）
        var measurements = _measurementService.GetAllMeasurementsAsync().Result;

        foreach (var measurement in measurements.Take(3))
        {
            var report = new Report
            {
                Id = _nextId++,
                ReportNumber = GenerateReportNumber(),
                MeasurementRecordId = measurement.Id,
                MeasurementRecord = measurement,
                Status = ReportStatus.Completed,
                DoctorOpinion = "患者步态基本正常，建议继续保持适量运动。",
                CreatedAt = measurement.MeasurementDate.AddDays(1),
                UpdatedAt = measurement.MeasurementDate.AddDays(1)
            };
            _mockReports.Add(report);
        }
    }

    /// <inheritdoc/>
    public Task<List<Report>> GetReportsByPatientIdAsync(int patientId)
    {
        var reports = _mockReports
            .Where(r => r.MeasurementRecord?.PatientId == patientId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
        return Task.FromResult(reports);
    }

    /// <inheritdoc/>
    public Task<Report?> GetReportByMeasurementIdAsync(int measurementRecordId)
    {
        var report = _mockReports.FirstOrDefault(r => r.MeasurementRecordId == measurementRecordId);
        return Task.FromResult(report);
    }

    /// <inheritdoc/>
    public Task<Report?> GetReportByIdAsync(int id)
    {
        var report = _mockReports.FirstOrDefault(r => r.Id == id);
        return Task.FromResult(report);
    }

    /// <inheritdoc/>
    public Task<int> CreateReportAsync(Report report)
    {
        report.Id = _nextId++;
        report.CreatedAt = DateTime.Now;
        report.UpdatedAt = DateTime.Now;
        _mockReports.Add(report);
        return Task.FromResult(report.Id);
    }

    /// <inheritdoc/>
    public Task<bool> UpdateReportAsync(Report report)
    {
        var existing = _mockReports.FirstOrDefault(r => r.Id == report.Id);
        if (existing == null) return Task.FromResult(false);

        existing.DoctorOpinion = report.DoctorOpinion;
        existing.Status = report.Status;
        existing.UpdatedAt = DateTime.Now;

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteReportAsync(int id)
    {
        var report = _mockReports.FirstOrDefault(r => r.Id == id);
        if (report == null) return Task.FromResult(false);

        _mockReports.Remove(report);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<string> GeneratePdfAsync(int reportId)
    {
        // TODO: 使用 ToolHelper.DataProcessing.PdfHelper 生成PDF
        var report = _mockReports.FirstOrDefault(r => r.Id == reportId);
        if (report == null) return Task.FromResult(string.Empty);

        // 占位：返回临时文件路径
        var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Report_{report.ReportNumber}.pdf");
        return Task.FromResult(tempPath);
    }

    /// <inheritdoc/>
    public Task<bool> PrintReportAsync(int reportId)
    {
        // TODO: 使用 PrintHelper 打印报告
        var report = _mockReports.FirstOrDefault(r => r.Id == reportId);
        if (report == null) return Task.FromResult(false);

        report.Status = ReportStatus.Printed;
        report.UpdatedAt = DateTime.Now;

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> UpdateReportStatusAsync(int reportId, ReportStatus status)
    {
        var report = _mockReports.FirstOrDefault(r => r.Id == reportId);
        if (report == null) return Task.FromResult(false);

        report.Status = status;
        report.UpdatedAt = DateTime.Now;

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public string GenerateReportNumber()
    {
        // 格式：RPT-YYYYMMDD-XXXX
        var sequence = _reportSequence++;
        return $"RPT-{DateTime.Now:yyyyMMdd}-{sequence:D4}";
    }

    /// <summary>
    /// 获取报告列表（带筛选）
    /// </summary>
    public Task<List<Report>> GetReportsAsync(string? patientName, DateTime? startDate, DateTime? endDate)
    {
        IEnumerable<Report> query = _mockReports;

        // 按患者姓名筛选
        if (!string.IsNullOrWhiteSpace(patientName))
        {
            var nameLower = patientName.Trim().ToLower();
            query = query.Where(r => r.MeasurementRecord?.Patient?.Name?.ToLower().Contains(nameLower) == true);
        }

        // 按开始日期筛选
        if (startDate.HasValue)
        {
            var start = startDate.Value.Date;
            query = query.Where(r => r.CreatedAt >= start);
        }

        // 按结束日期筛选
        if (endDate.HasValue)
        {
            var end = endDate.Value.Date.AddDays(1);
            query = query.Where(r => r.CreatedAt < end);
        }

        var reports = query.OrderByDescending(r => r.CreatedAt).ToList();
        return Task.FromResult(reports);
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
            existing.UpdatedAt = DateTime.Now;
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
                Status = ReportStatus.Draft,
                DoctorOpinion = string.Empty,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            await CreateReportAsync(report);
            _logHelper?.Information($"创建报告：ID={report.Id}, Number={report.ReportNumber}");
            return report;
        }
    }
}
