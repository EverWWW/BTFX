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
