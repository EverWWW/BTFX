using BTFX.Common;
using BTFX.Models;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 测量服务接口
/// </summary>
public interface IMeasurementService
{
    /// <summary>
    /// 获取患者的所有测量记录
    /// </summary>
    /// <param name="patientId">患者ID</param>
    /// <returns>测量记录列表</returns>
    Task<List<MeasurementRecord>> GetMeasurementsByPatientIdAsync(int patientId);

    /// <summary>
    /// 根据ID获取测量记录
    /// </summary>
    /// <param name="id">测量记录ID</param>
    /// <returns>测量记录</returns>
    Task<MeasurementRecord?> GetMeasurementByIdAsync(int id);

    /// <summary>
    /// 创建新的测量记录
    /// </summary>
    /// <param name="record">测量记录</param>
    /// <returns>新增的记录ID</returns>
    Task<int> CreateMeasurementAsync(MeasurementRecord record);

    /// <summary>
    /// 更新测量记录
    /// </summary>
    /// <param name="record">测量记录</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateMeasurementAsync(MeasurementRecord record);

    /// <summary>
    /// 删除测量记录
    /// </summary>
    /// <param name="id">测量记录ID</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteMeasurementAsync(int id);

    /// <summary>
    /// 批量删除测量记录
    /// </summary>
    /// <param name="ids">测量记录ID列表</param>
    /// <returns>成功删除的数量</returns>
    Task<int> DeleteMeasurementsAsync(IEnumerable<int> ids);

    /// <summary>
    /// 保存步态参数
    /// </summary>
    /// <param name="parameters">步态参数</param>
    /// <returns>参数ID</returns>
    Task<int> SaveGaitParametersAsync(GaitParameters parameters);

    /// <summary>
    /// 获取步态参数
    /// </summary>
    /// <param name="measurementRecordId">测量记录ID</param>
    /// <returns>步态参数</returns>
    Task<GaitParameters?> GetGaitParametersAsync(int measurementRecordId);

    /// <summary>
    /// 更新测量状态
    /// </summary>
    /// <param name="measurementId">测量记录ID</param>
    /// <param name="status">新状态</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateMeasurementStatusAsync(int measurementId, MeasurementStatus status);

    /// <summary>
    /// 获取所有测量记录
    /// </summary>
    /// <returns>测量记录列表</returns>
    Task<List<MeasurementRecord>> GetAllMeasurementsAsync();

        /// <summary>
        /// 获取测量记录（带分页和筛选）
        /// </summary>
        /// <param name="patientName">患者姓名（模糊搜索）</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <param name="status">测量状态（null表示全部）</param>
        /// <param name="page">页码（从1开始）</param>
        /// <param name="pageSize">每页记录数</param>
        /// <returns>测量记录列表和总记录数</returns>
        Task<(List<MeasurementRecord> Records, int TotalCount)> GetMeasurementsPagedAsync(
            string? patientName,
            DateTime? startDate,
            DateTime? endDate,
            MeasurementStatus? status,
            int page,
            int pageSize);

        /// <summary>
        /// 根据ID列表批量获取测量记录
        /// </summary>
        /// <param name="ids">测量记录ID列表</param>
        /// <returns>测量记录列表</returns>
        Task<List<MeasurementRecord>> GetMeasurementsByIdsAsync(List<int> ids);

        /// <summary>
        /// 获取测量记录总数
        /// </summary>
        /// <returns>总记录数</returns>
        Task<int> GetMeasurementCountAsync();
    }
