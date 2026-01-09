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
    Task<bool> UpdateMeasurementStatusAsync(int measurementId, Common.MeasurementStatus status);
}
