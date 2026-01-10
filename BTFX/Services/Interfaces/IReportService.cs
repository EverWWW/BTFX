using BTFX.Common;
using BTFX.Models;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 报告服务接口
/// </summary>
public interface IReportService
{
    /// <summary>
    /// 获取患者的所有报告
    /// </summary>
    /// <param name="patientId">患者ID</param>
    /// <returns>报告列表</returns>
    Task<List<Report>> GetReportsByPatientIdAsync(int patientId);

    /// <summary>
    /// 根据测量记录获取报告
    /// </summary>
    /// <param name="measurementRecordId">测量记录ID</param>
    /// <returns>报告</returns>
    Task<Report?> GetReportByMeasurementIdAsync(int measurementRecordId);

    /// <summary>
    /// 根据ID获取报告
    /// </summary>
    /// <param name="id">报告ID</param>
    /// <returns>报告</returns>
    Task<Report?> GetReportByIdAsync(int id);

    /// <summary>
    /// 创建报告
    /// </summary>
    /// <param name="report">报告信息</param>
    /// <returns>新增的报告ID</returns>
    Task<int> CreateReportAsync(Report report);

    /// <summary>
    /// 更新报告
    /// </summary>
    /// <param name="report">报告信息</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateReportAsync(Report report);

    /// <summary>
    /// 删除报告
    /// </summary>
    /// <param name="id">报告ID</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteReportAsync(int id);

    /// <summary>
    /// 生成PDF报告
    /// </summary>
    /// <param name="reportId">报告ID</param>
    /// <returns>PDF文件路径</returns>
    Task<string> GeneratePdfAsync(int reportId);

    /// <summary>
    /// 打印报告
    /// </summary>
    /// <param name="reportId">报告ID</param>
    /// <returns>是否成功</returns>
    Task<bool> PrintReportAsync(int reportId);

    /// <summary>
    /// 更新报告状态
    /// </summary>
    /// <param name="reportId">报告ID</param>
    /// <param name="status">新状态</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateReportStatusAsync(int reportId, ReportStatus status);

    /// <summary>
    /// 生成报告编号
    /// </summary>
    /// <returns>报告编号</returns>
    string GenerateReportNumber();

    /// <summary>
    /// 获取报告列表（带筛选）
    /// </summary>
    /// <param name="patientName">患者姓名（模糊搜索）</param>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <returns>报告列表</returns>
    Task<List<Report>> GetReportsAsync(string? patientName, DateTime? startDate, DateTime? endDate);

    /// <summary>
    /// 生成报告
    /// </summary>
    /// <param name="measurementRecordId">测量记录ID</param>
    /// <param name="operatorId">操作员ID</param>
    /// <returns>生成的报告</returns>
    Task<Report?> GenerateReportAsync(int measurementRecordId, int operatorId);
}
