using BTFX.Common;
using BTFX.Data;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 测量服务实现
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var records = await db.QueryAsync<MeasurementRecord>(@"
                SELECT Id, PatientId, UserId AS OperatorId, MeasurementDate, Status, VideoPath AS VideoFilePath,
                       Duration AS DurationSeconds, Remark, IsGuestData, CreatedAt, UpdatedAt
                FROM MeasurementRecords 
                WHERE PatientId = @PatientId
                ORDER BY MeasurementDate DESC
            ", new { PatientId = patientId });

            var result = records.ToList();

            // 加载关联的患者信息
            foreach (var record in result)
            {
                record.Patient = await GetPatientByIdAsync(record.PatientId);
            }

            return result;
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var record = await db.QueryFirstOrDefaultAsync<MeasurementRecord>(@"
                SELECT Id, PatientId, UserId AS OperatorId, MeasurementDate, Status, VideoPath AS VideoFilePath,
                       Duration AS DurationSeconds, Remark, IsGuestData, CreatedAt, UpdatedAt
                FROM MeasurementRecords 
                WHERE Id = @Id
            ", new { Id = id });

            if (record != null)
            {
                // 加载关联数据
                record.Patient = await GetPatientByIdAsync(record.PatientId);
                record.GaitParameters = await GetGaitParametersAsync(record.Id);
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);
            var measurementDate = record.MeasurementDate.ToString(Constants.DATETIME_FORMAT);

            var id = await db.InsertAndGetIdAsync(@"
                INSERT INTO MeasurementRecords (PatientId, UserId, MeasurementDate, Status, VideoPath, 
                                               Duration, Remark, IsGuestData, CreatedAt, UpdatedAt)
                VALUES (@PatientId, @UserId, @MeasurementDate, @Status, @VideoPath, 
                        @Duration, @Remark, @IsGuestData, @CreatedAt, @UpdatedAt)
            ", new
            {
                record.PatientId,
                UserId = record.OperatorId,
                MeasurementDate = measurementDate,
                Status = (int)record.Status,
                VideoPath = record.VideoFilePath,
                Duration = record.DurationSeconds,
                record.Remark,
                IsGuestData = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);

            var affected = await db.ExecuteNonQueryAsync(@"
                UPDATE MeasurementRecords 
                SET Status = @Status, VideoPath = @VideoPath, Duration = @Duration, 
                    Remark = @Remark, UpdatedAt = @UpdatedAt
                WHERE Id = @Id
            ", new
            {
                record.Id,
                Status = (int)record.Status,
                VideoPath = record.VideoFilePath,
                Duration = record.DurationSeconds,
                record.Remark,
                UpdatedAt = now
            });

            return affected > 0;
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            // 先删除关联的步态参数
            await db.ExecuteNonQueryAsync("DELETE FROM GaitParameters WHERE MeasurementId = @Id", new { Id = id });

            // 再删除测量记录
            var affected = await db.ExecuteNonQueryAsync("DELETE FROM MeasurementRecords WHERE Id = @Id", new { Id = id });

            if (affected > 0)
            {
                _logHelper?.Information($"删除测量记录成功: Id={id}");
            }

            return affected > 0;
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);

            // 检查是否已存在
            var existing = await db.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM GaitParameters WHERE MeasurementId = @MeasurementId",
                new { MeasurementId = parameters.MeasurementRecordId });

            if (existing > 0)
            {
                // 更新
                await db.ExecuteNonQueryAsync(@"
                    UPDATE GaitParameters 
                    SET StrideLengthLeft = @StrideLengthLeft, StrideLengthRight = @StrideLengthRight,
                        Cadence = @Cadence, Velocity = @Velocity,
                        StancePhaseLeft = @StancePhaseLeft, StancePhaseRight = @StancePhaseRight,
                        SwingPhaseLeft = @SwingPhaseLeft, SwingPhaseRight = @SwingPhaseRight,
                        DoubleSupport = @DoubleSupport, SymmetryIndex = @SymmetryIndex
                    WHERE MeasurementId = @MeasurementId
                ", new
                {
                    MeasurementId = parameters.MeasurementRecordId,
                    parameters.StrideLengthLeft,
                    parameters.StrideLengthRight,
                    parameters.Cadence,
                    parameters.Velocity,
                    parameters.StancePhaseLeft,
                    parameters.StancePhaseRight,
                    parameters.SwingPhaseLeft,
                    parameters.SwingPhaseRight,
                    parameters.DoubleSupport,
                    parameters.SymmetryIndex
                });

                return parameters.Id;
            }
            else
            {
                // 插入
                var id = await db.InsertAndGetIdAsync(@"
                    INSERT INTO GaitParameters (MeasurementId, StrideLengthLeft, StrideLengthRight,
                        Cadence, Velocity, StancePhaseLeft, StancePhaseRight, 
                        SwingPhaseLeft, SwingPhaseRight, DoubleSupport, SymmetryIndex, CreatedAt)
                    VALUES (@MeasurementId, @StrideLengthLeft, @StrideLengthRight,
                        @Cadence, @Velocity, @StancePhaseLeft, @StancePhaseRight,
                        @SwingPhaseLeft, @SwingPhaseRight, @DoubleSupport, @SymmetryIndex, @CreatedAt)
                ", new
                {
                    MeasurementId = parameters.MeasurementRecordId,
                    parameters.StrideLengthLeft,
                    parameters.StrideLengthRight,
                    parameters.Cadence,
                    parameters.Velocity,
                    parameters.StancePhaseLeft,
                    parameters.StancePhaseRight,
                    parameters.SwingPhaseLeft,
                    parameters.SwingPhaseRight,
                    parameters.DoubleSupport,
                    parameters.SymmetryIndex,
                    CreatedAt = now
                });

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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            return await db.QueryFirstOrDefaultAsync<GaitParameters>(@"
                SELECT Id, MeasurementId AS MeasurementRecordId, StrideLengthLeft, StrideLengthRight,
                       StepLengthLeft, StepLengthRight, Cadence, Velocity,
                       StancePhaseLeft, StancePhaseRight, SwingPhaseLeft, SwingPhaseRight,
                       DoubleSupport, SingleSupport, SymmetryIndex, ParametersJson, CreatedAt
                FROM GaitParameters 
                WHERE MeasurementId = @MeasurementId
            ", new { MeasurementId = measurementRecordId });
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);

            var affected = await db.ExecuteNonQueryAsync(@"
                UPDATE MeasurementRecords 
                SET Status = @Status, UpdatedAt = @UpdatedAt
                WHERE Id = @Id
            ", new
            {
                Id = measurementId,
                Status = (int)status,
                UpdatedAt = now
            });

            return affected > 0;
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var records = await db.QueryAsync<MeasurementRecord>(@"
                SELECT Id, PatientId, UserId AS OperatorId, MeasurementDate, Status, VideoPath AS VideoFilePath,
                       Duration AS DurationSeconds, Remark, IsGuestData, CreatedAt, UpdatedAt
                FROM MeasurementRecords 
                ORDER BY MeasurementDate DESC
            ");

            var result = records.ToList();

            // 加载关联的患者信息
            foreach (var record in result)
            {
                record.Patient = await GetPatientByIdAsync(record.PatientId);
            }

            return result;
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
                whereClause += " AND m.MeasurementDate >= @StartDate";
                parameters["StartDate"] = startDate.Value.ToString(Constants.DATE_FORMAT);
            }

            if (endDate.HasValue)
            {
                whereClause += " AND m.MeasurementDate < @EndDate";
                parameters["EndDate"] = endDate.Value.AddDays(1).ToString(Constants.DATE_FORMAT);
            }

            if (status.HasValue)
            {
                whereClause += " AND m.Status = @Status";
                parameters["Status"] = (int)status.Value;
            }

            // 查询总数
            var countSql = $@"
                SELECT COUNT(*) FROM MeasurementRecords m
                LEFT JOIN Patients p ON m.PatientId = p.Id
                {whereClause}
            ";
            var totalCount = await db.ExecuteScalarAsync<int>(countSql, parameters);

            // 计算偏移量
            var offset = (page - 1) * pageSize;

            // 查询数据
            var dataSql = $@"
                SELECT m.Id, m.PatientId, m.UserId AS OperatorId, m.MeasurementDate, m.Status, 
                       m.VideoPath AS VideoFilePath, m.Duration AS DurationSeconds, 
                       m.Remark, m.IsGuestData, m.CreatedAt, m.UpdatedAt
                FROM MeasurementRecords m
                LEFT JOIN Patients p ON m.PatientId = p.Id
                {whereClause}
                ORDER BY m.MeasurementDate DESC
                LIMIT @PageSize OFFSET @Offset
            ";

            parameters["PageSize"] = pageSize;
            parameters["Offset"] = offset;

            var records = await db.QueryAsync<MeasurementRecord>(dataSql, parameters);
            var result = records.ToList();

            // 加载关联数据
            foreach (var record in result)
            {
                record.Patient = await GetPatientByIdAsync(record.PatientId);
                record.GaitParameters = await GetGaitParametersAsync(record.Id);
            }

            return (result, totalCount);
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            return await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM MeasurementRecords");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("获取测量记录数量失败", ex);
            return 0;
        }
    }

    /// <summary>
    /// 获取患者信息（辅助方法）
    /// </summary>
    private async Task<Patient?> GetPatientByIdAsync(int patientId)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            return await db.QueryFirstOrDefaultAsync<Patient>(@"
                SELECT Id, Name, Gender, BirthDate, Phone, IdNumber, Height, Weight, 
                       Address, MedicalHistory, Remark, Status, CreatedBy, CreatedAt, UpdatedAt 
                FROM Patients 
                WHERE Id = @Id
            ", new { Id = patientId });
        }
        catch
        {
            return null;
        }
    }
}
