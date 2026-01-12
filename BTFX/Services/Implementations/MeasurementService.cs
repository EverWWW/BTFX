using BTFX.Common;
using BTFX.Data;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 测量服务实现（使用 SqlSugar）
/// </summary>
public class MeasurementService : IMeasurementService
{
    private readonly ILogHelper? _logHelper;

    /// <summary>
    /// 构造函数
    /// </summary>
    public MeasurementService()
    {
        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }
    }

    /// <inheritdoc/>
    public async Task<List<MeasurementRecord>> GetMeasurementsByPatientIdAsync(int patientId)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var records = await db.Queryable<MeasurementRecord>()
                .Where(m => m.PatientId == patientId)
                .OrderByDescending(m => m.MeasurementDate)
                .ToListAsync();

            // 加载关联的患者信息
            foreach (var record in records)
            {
                record.Patient = await db.GetByIdAsync<Patient>(record.PatientId);
            }

            return records;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取患者测量记录失败: PatientId={patientId}", ex);
            return new List<MeasurementRecord>();
        }
    }

    /// <inheritdoc/>
    public async Task<MeasurementRecord?> GetMeasurementByIdAsync(int id)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var record = await db.GetByIdAsync<MeasurementRecord>(id);

            if (record != null)
            {
                // 加载关联数据
                record.Patient = await db.GetByIdAsync<Patient>(record.PatientId);
                record.GaitParameters = await db.GetFirstAsync<GaitParameters>(
                    g => g.MeasurementRecordId == record.Id);
            }

            return record;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取测量记录失败: Id={id}", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<int> CreateMeasurementAsync(MeasurementRecord record)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var now = DateTime.Now;
            record.CreatedAt = now;
            record.UpdatedAt = now;

            var id = await db.InsertReturnIdentityAsync(record);

            _logHelper?.Information($"创建测量记录成功: Id={id}");
            return (int)id;
        }
        catch (Exception ex)
        {
            _logHelper?.Error("创建测量记录失败", ex);
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateMeasurementAsync(MeasurementRecord record)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            record.UpdatedAt = DateTime.Now;

            var count = await db.UpdateAsync<MeasurementRecord>(
                m => new MeasurementRecord
                {
                    Status = record.Status,
                    VideoFilePath = record.VideoFilePath,
                    DurationSeconds = record.DurationSeconds,
                    Remark = record.Remark,
                    UpdatedAt = record.UpdatedAt
                },
                m => m.Id == record.Id);

            return count > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"更新测量记录失败: Id={record.Id}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteMeasurementAsync(int id)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            // 先删除关联的步态参数
            await db.DeleteAsync<GaitParameters>(g => g.MeasurementRecordId == id);

            // 再删除测量记录
            var success = await db.DeleteByIdAsync<MeasurementRecord>(id);

            if (success)
            {
                _logHelper?.Information($"删除测量记录成功: Id={id}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除测量记录失败: Id={id}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<int> DeleteMeasurementsAsync(IEnumerable<int> ids)
    {
        var count = 0;
        foreach (var id in ids)
        {
            if (await DeleteMeasurementAsync(id))
            {
                count++;
            }
        }
        return count;
    }

    /// <inheritdoc/>
    public async Task<int> SaveGaitParametersAsync(GaitParameters parameters)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var now = DateTime.Now;

            // 检查是否已存在
            var existing = await db.GetFirstAsync<GaitParameters>(
                g => g.MeasurementRecordId == parameters.MeasurementRecordId);

            if (existing != null)
            {
                // 更新
                parameters.Id = existing.Id;
                await db.UpdateAsync(parameters);
                return parameters.Id;
            }
            else
            {
                // 插入
                parameters.CreatedAt = now;
                var id = await db.InsertReturnIdentityAsync(parameters);
                return (int)id;
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"保存步态参数失败: MeasurementId={parameters.MeasurementRecordId}", ex);
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<GaitParameters?> GetGaitParametersAsync(int measurementRecordId)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            return await db.GetFirstAsync<GaitParameters>(
                g => g.MeasurementRecordId == measurementRecordId);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取步态参数失败: MeasurementId={measurementRecordId}", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateMeasurementStatusAsync(int measurementId, MeasurementStatus status)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var now = DateTime.Now;

            var count = await db.UpdateAsync<MeasurementRecord>(
                m => new MeasurementRecord
                {
                    Status = status,
                    UpdatedAt = now
                },
                m => m.Id == measurementId);

            return count > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"更新测量状态失败: Id={measurementId}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<List<MeasurementRecord>> GetAllMeasurementsAsync()
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var records = await db.Queryable<MeasurementRecord>()
                .OrderByDescending(m => m.MeasurementDate)
                .ToListAsync();

            // 加载关联的患者信息
            foreach (var record in records)
            {
                record.Patient = await db.GetByIdAsync<Patient>(record.PatientId);
            }

            return records;
        }
        catch (Exception ex)
        {
            _logHelper?.Error("获取所有测量记录失败", ex);
            return new List<MeasurementRecord>();
        }
    }

    /// <inheritdoc/>
    public async Task<(List<MeasurementRecord> Records, int TotalCount)> GetMeasurementsPagedAsync(
        string? patientName,
        DateTime? startDate,
        DateTime? endDate,
        MeasurementStatus? status,
        int page,
        int pageSize)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            // 构建基础查询
            var baseQuery = db.Queryable<MeasurementRecord>();

            if (startDate.HasValue)
            {
                baseQuery = baseQuery.Where(m => m.MeasurementDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                var endDateValue = endDate.Value.AddDays(1);
                baseQuery = baseQuery.Where(m => m.MeasurementDate < endDateValue);
            }

            if (status.HasValue)
            {
                baseQuery = baseQuery.Where(m => m.Status == status.Value);
            }

            // 如果有患者名称筛选，先查出符合条件的患者ID
            if (!string.IsNullOrWhiteSpace(patientName))
            {
                var patientIds = await db.Queryable<Patient>()
                    .Where(p => p.Name.Contains(patientName))
                    .Select(p => p.Id)
                    .ToListAsync();

                if (patientIds.Any())
                {
                    baseQuery = baseQuery.Where(m => patientIds.Contains(m.PatientId));
                }
                else
                {
                    return (new List<MeasurementRecord>(), 0);
                }
            }

            // 查询总数
            var totalCount = await baseQuery.CountAsync();

            // 分页查询
            var records = await baseQuery
                .OrderByDescending(m => m.MeasurementDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 加载关联数据
            foreach (var record in records)
            {
                record.Patient = await db.GetByIdAsync<Patient>(record.PatientId);
                record.GaitParameters = await db.GetFirstAsync<GaitParameters>(
                    g => g.MeasurementRecordId == record.Id);
            }

            return (records, totalCount);
        }
        catch (Exception ex)
        {
            _logHelper?.Error("分页查询测量记录失败", ex);
            return (new List<MeasurementRecord>(), 0);
        }
    }

    /// <inheritdoc/>
    public async Task<List<MeasurementRecord>> GetMeasurementsByIdsAsync(List<int> ids)
    {
        try
        {
            if (ids == null || ids.Count == 0)
            {
                return new List<MeasurementRecord>();
            }

            var result = new List<MeasurementRecord>();
            foreach (var id in ids)
            {
                var record = await GetMeasurementByIdAsync(id);
                if (record != null)
                {
                    result.Add(record);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logHelper?.Error("批量获取测量记录失败", ex);
            return new List<MeasurementRecord>();
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetMeasurementCountAsync()
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            return await db.CountAsync<MeasurementRecord>();
        }
        catch (Exception ex)
        {
            _logHelper?.Error("获取测量记录数量失败", ex);
            return 0;
        }
    }
}
