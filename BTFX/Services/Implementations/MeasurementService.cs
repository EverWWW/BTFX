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

    // 模拟数据
    private static readonly List<MeasurementRecord> _mockRecords = GenerateMockData();

    /// <summary>
    /// 生成模拟数据
    /// </summary>
    private static List<MeasurementRecord> GenerateMockData()
    {
        var random = new Random(42);
        var records = new List<MeasurementRecord>();
        var statuses = new[] { MeasurementStatus.Pending, MeasurementStatus.Completed, MeasurementStatus.Cancelled, MeasurementStatus.Failed };
        var names = new[] { "张三", "李四", "王五", "赵六", "钱七", "孙八", "周九", "吴十", "郑十一", "王小明" };
        var genders = new[] { Gender.Male, Gender.Female };

        for (int i = 1; i <= 50; i++)
        {
            var name = names[random.Next(names.Length)];
            var measurementDate = DateTime.Now.AddDays(-random.Next(0, 90)).AddHours(-random.Next(0, 24));
            var status = statuses[random.Next(statuses.Length)];

            var patient = new Patient
            {
                Id = i,
                Name = name,
                Gender = genders[random.Next(genders.Length)],
                BirthDate = DateTime.Now.AddYears(-random.Next(20, 80)),
                Phone = $"138{random.Next(10000000, 99999999)}",
                Height = random.Next(150, 190),
                Weight = random.Next(45, 95),
                Status = PatientStatus.Active
            };

            GaitParameters? gaitParams = null;
            if (status == MeasurementStatus.Completed)
            {
                gaitParams = new GaitParameters
                {
                    Id = i,
                    MeasurementRecordId = i,
                    StrideLengthLeft = Math.Round(random.NextDouble() * 50 + 80, 2),
                    StrideLengthRight = Math.Round(random.NextDouble() * 50 + 80, 2),
                    Cadence = Math.Round(random.NextDouble() * 40 + 80, 1),
                    Velocity = Math.Round(random.NextDouble() * 0.8 + 0.6, 2),
                    StancePhaseLeft = Math.Round(random.NextDouble() * 10 + 55, 1),
                    StancePhaseRight = Math.Round(random.NextDouble() * 10 + 55, 1),
                    DoubleSupport = Math.Round(random.NextDouble() * 10 + 10, 1),
                    CreatedAt = measurementDate,
                    UpdatedAt = measurementDate
                };
            }

            records.Add(new MeasurementRecord
            {
                Id = i,
                PatientId = i,
                Patient = patient,
                MeasurementDate = measurementDate,
                Status = status,
                DurationSeconds = status == MeasurementStatus.Completed ? random.Next(30, 180) : null,
                OperatorId = 1,
                Operator = new User { Id = 1, Username = "admin", Name = "管理员", Role = UserRole.Administrator },
                GaitParametersId = gaitParams?.Id,
                GaitParameters = gaitParams,
                Remark = i % 5 == 0 ? "测试备注" : null,
                CreatedAt = measurementDate,
                UpdatedAt = measurementDate
            });
        }

        return records;
    }

    /// <inheritdoc/>
    public Task<List<MeasurementRecord>> GetMeasurementsByPatientIdAsync(int patientId)
    {
        var records = _mockRecords.Where(r => r.PatientId == patientId).ToList();
        return Task.FromResult(records);
    }

    /// <inheritdoc/>
    public Task<MeasurementRecord?> GetMeasurementByIdAsync(int id)
    {
        var record = _mockRecords.FirstOrDefault(r => r.Id == id);
        return Task.FromResult(record);
    }

    /// <inheritdoc/>
    public Task<int> CreateMeasurementAsync(MeasurementRecord record)
    {
        var newId = _mockRecords.Count > 0 ? _mockRecords.Max(r => r.Id) + 1 : 1;
        record.Id = newId;
        record.CreatedAt = DateTime.Now;
        record.UpdatedAt = DateTime.Now;
        _mockRecords.Add(record);
        return Task.FromResult(newId);
    }

    /// <inheritdoc/>
    public Task<bool> UpdateMeasurementAsync(MeasurementRecord record)
    {
        var existing = _mockRecords.FirstOrDefault(r => r.Id == record.Id);
        if (existing == null) return Task.FromResult(false);

        existing.Status = record.Status;
        existing.DurationSeconds = record.DurationSeconds;
        existing.Remark = record.Remark;
        existing.UpdatedAt = DateTime.Now;
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteMeasurementAsync(int id)
    {
        var record = _mockRecords.FirstOrDefault(r => r.Id == id);
        if (record == null) return Task.FromResult(false);

        _mockRecords.Remove(record);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<int> DeleteMeasurementsAsync(IEnumerable<int> ids)
    {
        var idList = ids.ToList();
        var count = 0;
        foreach (var id in idList)
        {
            var record = _mockRecords.FirstOrDefault(r => r.Id == id);
            if (record != null)
            {
                _mockRecords.Remove(record);
                count++;
            }
        }
        return Task.FromResult(count);
    }

    /// <inheritdoc/>
    public Task<int> SaveGaitParametersAsync(GaitParameters parameters)
    {
        var newId = _mockRecords.Count > 0 ? _mockRecords.Max(r => r.Id) + 1 : 1;
        parameters.Id = newId;
        return Task.FromResult(newId);
    }

    /// <inheritdoc/>
    public Task<GaitParameters?> GetGaitParametersAsync(int measurementRecordId)
    {
        var record = _mockRecords.FirstOrDefault(r => r.Id == measurementRecordId);
        return Task.FromResult(record?.GaitParameters);
    }

    /// <inheritdoc/>
    public Task<bool> UpdateMeasurementStatusAsync(int measurementId, MeasurementStatus status)
    {
        var record = _mockRecords.FirstOrDefault(r => r.Id == measurementId);
        if (record == null) return Task.FromResult(false);

        record.Status = status;
        record.UpdatedAt = DateTime.Now;
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<List<MeasurementRecord>> GetAllMeasurementsAsync()
    {
            return Task.FromResult(_mockRecords.ToList());
        }

        /// <inheritdoc/>
        public Task<(List<MeasurementRecord> Records, int TotalCount)> GetMeasurementsPagedAsync(
            string? patientName,
            DateTime? startDate,
            DateTime? endDate,
            MeasurementStatus? status,
            int page,
            int pageSize)
        {
            IEnumerable<MeasurementRecord> query = _mockRecords;

            // 按患者姓名筛选
            if (!string.IsNullOrWhiteSpace(patientName))
            {
                var nameLower = patientName.Trim().ToLower();
                query = query.Where(r => r.Patient?.Name?.ToLower().Contains(nameLower) == true);
            }

            // 按开始日期筛选
            if (startDate.HasValue)
            {
                var start = startDate.Value.Date;
                query = query.Where(r => r.MeasurementDate >= start);
            }

            // 按结束日期筛选
            if (endDate.HasValue)
            {
                var end = endDate.Value.Date.AddDays(1);
                query = query.Where(r => r.MeasurementDate < end);
            }

            // 按状态筛选
            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

                    // 获取总数
                    var totalCount = query.Count();

                    // 按测量日期降序排序并分页
                    var records = query
                        .OrderByDescending(r => r.MeasurementDate)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    return Task.FromResult((records, totalCount));
                }

                /// <inheritdoc/>
                public Task<List<MeasurementRecord>> GetMeasurementsByIdsAsync(List<int> ids)
                {
                    var records = _mockRecords.Where(r => ids.Contains(r.Id)).ToList();
                    return Task.FromResult(records);
                }

                /// <inheritdoc/>
                public Task<int> GetMeasurementCountAsync()
                {
                    return Task.FromResult(_mockRecords.Count);
                }
            }
