using BTFX.Common;
using BTFX.Data;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 患者服务实现
/// </summary>
public class PatientService : IPatientService
{
    private readonly ILogHelper? _logHelper;

    /// <summary>
    /// 构造函数
    /// </summary>
    public PatientService()
    {
        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }
    }

    /// <inheritdoc/>
    public async Task<List<Patient>> GetAllPatientsAsync()
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var patients = await db.QueryAsync<Patient>(@"
                SELECT Id, Name, Gender, BirthDate, Phone, IdNumber, Height, Weight, 
                       Address, MedicalHistory, Remark, Status, CreatedBy, CreatedAt, UpdatedAt 
                FROM Patients 
                WHERE Status = 0
                ORDER BY CreatedAt DESC
            ");

            return patients.ToList();
        }
        catch (Exception ex)
        {
            _logHelper?.Error("获取患者列表失败", ex);
            return new List<Patient>();
        }
    }

    /// <inheritdoc/>
    public async Task<(List<Patient> Patients, int TotalCount)> GetPatientsPagedAsync(
        int pageIndex, int pageSize, string? searchText = null)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            // 构建查询条件
            var whereClause = "WHERE Status = 0";
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var search = $"%{searchText}%";
                whereClause += " AND (Name LIKE @Search OR Phone LIKE @Search OR IdNumber LIKE @Search)";
                parameters["Search"] = search;
            }

            // 查询总数
            var countSql = $"SELECT COUNT(*) FROM Patients {whereClause}";
            var totalCount = await db.ExecuteScalarAsync<int>(countSql, parameters);

            // 计算偏移量（pageIndex 从 1 开始）
            var offset = (pageIndex - 1) * pageSize;

            // 查询数据
            var dataSql = $@"
                SELECT Id, Name, Gender, BirthDate, Phone, IdNumber, Height, Weight, 
                       Address, MedicalHistory, Remark, Status, CreatedBy, CreatedAt, UpdatedAt 
                FROM Patients 
                {whereClause}
                ORDER BY CreatedAt DESC
                LIMIT @PageSize OFFSET @Offset
            ";

            parameters["PageSize"] = pageSize;
            parameters["Offset"] = offset;

            var patients = await db.QueryAsync<Patient>(dataSql, parameters);

            return (patients.ToList(), totalCount);
        }
        catch (Exception ex)
        {
            _logHelper?.Error("分页查询患者失败", ex);
            return (new List<Patient>(), 0);
        }
    }

    /// <inheritdoc/>
    public async Task<Patient?> GetPatientByIdAsync(int id)
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
            ", new { Id = id });
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取患者失败: Id={id}", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<int> AddPatientAsync(Patient patient)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);
            var birthDate = patient.BirthDate?.ToString(Constants.DATE_FORMAT);

            var id = await db.InsertAndGetIdAsync(@"
                INSERT INTO Patients (Name, Gender, BirthDate, Phone, IdNumber, Height, Weight, 
                                     Address, MedicalHistory, Remark, Status, CreatedBy, CreatedAt, UpdatedAt)
                VALUES (@Name, @Gender, @BirthDate, @Phone, @IdNumber, @Height, @Weight, 
                        @Address, @MedicalHistory, @Remark, @Status, @CreatedBy, @CreatedAt, @UpdatedAt)
            ", new
            {
                patient.Name,
                Gender = (int)patient.Gender,
                BirthDate = birthDate,
                patient.Phone,
                patient.IdNumber,
                patient.Height,
                patient.Weight,
                patient.Address,
                patient.MedicalHistory,
                patient.Remark,
                Status = (int)PatientStatus.Active,
                patient.CreatedBy,
                CreatedAt = now,
                UpdatedAt = now
            });

            _logHelper?.Information($"添加患者成功: Id={id}, Name={patient.Name}");
            return (int)id;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"添加患者失败: Name={patient.Name}", ex);
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UpdatePatientAsync(Patient patient)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);
            var birthDate = patient.BirthDate?.ToString(Constants.DATE_FORMAT);

            var affected = await db.ExecuteNonQueryAsync(@"
                UPDATE Patients 
                SET Name = @Name, Gender = @Gender, BirthDate = @BirthDate, Phone = @Phone, 
                    IdNumber = @IdNumber, Height = @Height, Weight = @Weight, Address = @Address,
                    MedicalHistory = @MedicalHistory, Remark = @Remark, UpdatedAt = @UpdatedAt
                WHERE Id = @Id
            ", new
            {
                patient.Id,
                patient.Name,
                Gender = (int)patient.Gender,
                BirthDate = birthDate,
                patient.Phone,
                patient.IdNumber,
                patient.Height,
                patient.Weight,
                patient.Address,
                patient.MedicalHistory,
                patient.Remark,
                UpdatedAt = now
            });

            if (affected > 0)
            {
                _logHelper?.Information($"更新患者成功: Id={patient.Id}");
            }

            return affected > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"更新患者失败: Id={patient.Id}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeletePatientAsync(int id)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);

            // 逻辑删除：设置 Status = 1 (Deleted)
            var affected = await db.ExecuteNonQueryAsync(@"
                UPDATE Patients 
                SET Status = @Status, UpdatedAt = @UpdatedAt
                WHERE Id = @Id
            ", new
            {
                Id = id,
                Status = (int)PatientStatus.Deleted,
                UpdatedAt = now
            });

            if (affected > 0)
            {
                _logHelper?.Information($"删除患者成功: Id={id}");
            }

            return affected > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除患者失败: Id={id}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<List<Patient>> SearchPatientsAsync(string searchText)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return await GetAllPatientsAsync();
            }

            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var search = $"%{searchText}%";

            var patients = await db.QueryAsync<Patient>(@"
                SELECT Id, Name, Gender, BirthDate, Phone, IdNumber, Height, Weight, 
                       Address, MedicalHistory, Remark, Status, CreatedBy, CreatedAt, UpdatedAt 
                FROM Patients 
                WHERE Status = 0 
                  AND (Name LIKE @Search OR Phone LIKE @Search OR IdNumber LIKE @Search)
                ORDER BY CreatedAt DESC
            ", new { Search = search });

            return patients.ToList();
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"搜索患者失败: searchText={searchText}", ex);
            return new List<Patient>();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsPatientExistsAsync(string phone, int? excludeId = null)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            string sql;
            object parameters;

            if (excludeId.HasValue)
            {
                sql = "SELECT COUNT(*) FROM Patients WHERE Phone = @Phone AND Status = 0 AND Id != @ExcludeId";
                parameters = new { Phone = phone, ExcludeId = excludeId.Value };
            }
            else
            {
                sql = "SELECT COUNT(*) FROM Patients WHERE Phone = @Phone AND Status = 0";
                parameters = new { Phone = phone };
            }

            var count = await db.ExecuteScalarAsync<int>(sql, parameters);
            return count > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"检查患者电话失败: Phone={phone}", ex);
            return true; // 出错时返回 true，防止重复添加
        }
    }
}
