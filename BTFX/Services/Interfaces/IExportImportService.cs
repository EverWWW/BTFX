using BTFX.Common;
using BTFX.Models;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 导出导入服务接口
/// </summary>
public interface IExportImportService
{
    /// <summary>
    /// 导出患者数据
    /// </summary>
    /// <param name="patients">患者列表</param>
    /// <param name="format">导出格式</param>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否成功</returns>
    Task<bool> ExportPatientsAsync(List<Patient> patients, ExportFormat format, string filePath);

    /// <summary>
    /// 导出测量数据
    /// </summary>
    /// <param name="measurements">测量记录列表</param>
    /// <param name="format">导出格式</param>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否成功</returns>
    Task<bool> ExportMeasurementsAsync(List<MeasurementRecord> measurements, ExportFormat format, string filePath);

    /// <summary>
    /// 导出测量结果归档包。归档包包含测量、患者、分析结果、报告和算法输出文件，不包含原始采集/导入视频。
    /// </summary>
    /// <param name="measurements">测量记录列表</param>
    /// <param name="filePath">归档包路径（.btfxpkg 或 .zip）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>导出结果</returns>
    Task<MeasurementArchiveExportResult> ExportMeasurementArchiveAsync(
        List<MeasurementRecord> measurements,
        string filePath,
        IProgress<OperationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 导入测量结果归档包。导入后生成新的患者/测量/分析记录，并恢复可查看的结果文件。
    /// </summary>
    /// <param name="filePath">归档包路径（.btfxpkg 或 .zip）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>导入结果</returns>
    Task<MeasurementArchiveImportResult> ImportMeasurementArchiveAsync(
        string filePath,
        IProgress<OperationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 导入患者数据
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>导入的患者列表</returns>
    Task<List<Patient>> ImportPatientsAsync(string filePath);

    /// <summary>
    /// 导出报告为Excel
    /// </summary>
    /// <param name="reportId">报告ID</param>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否成功</returns>
    Task<bool> ExportReportToExcelAsync(int reportId, string filePath);
}

public sealed record MeasurementArchiveExportResult(
    bool Success,
    string Message,
    int ExportedCount,
    string? FilePath = null);

public sealed record MeasurementArchiveImportResult(
    bool Success,
    string Message,
    int ImportedCount,
    IReadOnlyList<int>? MeasurementIds = null);

public sealed record OperationProgressInfo(
    double Percent,
    string Stage,
    string Message,
    bool IsIndeterminate = false);
