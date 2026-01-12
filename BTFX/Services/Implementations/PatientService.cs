using BTFX.Common;
using BTFX.Data;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 患者服务实现（使用 SqlSugar）
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
            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            return await db.GetListAsync<Patient>(p => p.Status == PatientStatus.Active);
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
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            // 使用 SqlSugar 的分页查询
            var query = db.Queryable<Patient>()
                .Where(p => p.Status == PatientStatus.Active);

            // 添加搜索条件
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(p => 
                    p.Name.Contains(searchText) || 
                    p.Phone.Contains(searchText) || 
                    (p.IdNumber != null && p.IdNumber.Contains(searchText)));
            }

            // 排序
            query = query.OrderByDescending(p => p.CreatedAt);

            // 分页查询
            var totalCount = await query.CountAsync();
            var patients = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (patients, totalCount);
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
            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            return await db.GetByIdAsync<Patient>(id);
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
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var now = DateTime.Now;
            patient.Status = PatientStatus.Active;
            patient.CreatedAt = now;
            patient.UpdatedAt = now;

            var id = await db.InsertReturnIdentityAsync(patient);

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
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            patient.UpdatedAt = DateTime.Now;

            var success = await db.UpdateAsync(patient);

            if (success)
            {
                _logHelper?.Information($"更新患者成功: Id={patient.Id}");
            }

            return success;
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
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var now = DateTime.Now;

            // 逻辑删除：设置 Status = Deleted
            var count = await db.UpdateAsync<Patient>(
                p => new Patient 
                { 
                    Status = PatientStatus.Deleted,
                    UpdatedAt = now
                },
                p => p.Id == id);

            if (count > 0)
            {
                _logHelper?.Information($"删除患者成功: Id={id}");
            }

            return count > 0;
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

            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            return await db.Queryable<Patient>()
                .Where(p => p.Status == PatientStatus.Active)
                .Where(p => 
                    p.Name.Contains(searchText) || 
                    p.Phone.Contains(searchText) || 
                    (p.IdNumber != null && p.IdNumber.Contains(searchText)))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
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
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            if (excludeId.HasValue)
            {
                return await db.AnyAsync<Patient>(p => 
                    p.Phone == phone && 
                    p.Status == PatientStatus.Active && 
                    p.Id != excludeId.Value);
            }
            else
            {
                return await db.AnyAsync<Patient>(p => 
                    p.Phone == phone && 
                    p.Status == PatientStatus.Active);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"检查患者电话失败: Phone={phone}", ex);
            return true; // 出错时返回 true，防止重复添加
        }
    }
}
