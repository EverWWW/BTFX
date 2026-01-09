using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// 测量服务实现（占位实现，第四阶段完善）
/// </summary>
public class MeasurementService : IMeasurementService
{
    // TODO: 注入数据库服务

    /// <inheritdoc/>
    public Task<List<MeasurementRecord>> GetMeasurementsByPatientIdAsync(int patientId)
    {
        // TODO: 从数据库读取
        return Task.FromResult(new List<MeasurementRecord>());
    }

    /// <inheritdoc/>
    public Task<MeasurementRecord?> GetMeasurementByIdAsync(int id)
    {
        // TODO: 从数据库读取
        return Task.FromResult<MeasurementRecord?>(null);
    }

    /// <inheritdoc/>
    public Task<int> CreateMeasurementAsync(MeasurementRecord record)
    {
        // TODO: 插入数据库
        return Task.FromResult(0);
    }

    /// <inheritdoc/>
    public Task<bool> UpdateMeasurementAsync(MeasurementRecord record)
    {
        // TODO: 更新数据库
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteMeasurementAsync(int id)
    {
        // TODO: 删除数据库记录
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<int> SaveGaitParametersAsync(GaitParameters parameters)
    {
        // TODO: 保存到数据库
        return Task.FromResult(0);
    }

    /// <inheritdoc/>
    public Task<GaitParameters?> GetGaitParametersAsync(int measurementRecordId)
    {
        // TODO: 从数据库读取
        return Task.FromResult<GaitParameters?>(null);
    }

    /// <inheritdoc/>
    public Task<bool> UpdateMeasurementStatusAsync(int measurementId, MeasurementStatus status)
    {
        // TODO: 更新状态
        return Task.FromResult(false);
    }
}
