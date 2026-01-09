using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// 患者服务实现（占位实现，第四阶段完善）
/// </summary>
public class PatientService : IPatientService
{
    // TODO: 注入数据库服务

    /// <inheritdoc/>
    public Task<List<Patient>> GetAllPatientsAsync()
    {
        // TODO: 从数据库读取
        return Task.FromResult(new List<Patient>());
    }

    /// <inheritdoc/>
    public Task<(List<Patient> Patients, int TotalCount)> GetPatientsPagedAsync(int pageIndex, int pageSize, string? searchText = null)
    {
        // TODO: 从数据库分页读取
        return Task.FromResult((new List<Patient>(), 0));
    }

    /// <inheritdoc/>
    public Task<Patient?> GetPatientByIdAsync(int id)
    {
        // TODO: 从数据库读取
        return Task.FromResult<Patient?>(null);
    }

    /// <inheritdoc/>
    public Task<int> AddPatientAsync(Patient patient)
    {
        // TODO: 插入数据库
        return Task.FromResult(0);
    }

    /// <inheritdoc/>
    public Task<bool> UpdatePatientAsync(Patient patient)
    {
        // TODO: 更新数据库
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<bool> DeletePatientAsync(int id)
    {
        // TODO: 逻辑删除
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<List<Patient>> SearchPatientsAsync(string searchText)
    {
        // TODO: 搜索数据库
        return Task.FromResult(new List<Patient>());
    }

    /// <inheritdoc/>
    public Task<bool> IsPatientExistsAsync(string phone, int? excludeId = null)
    {
        // TODO: 检查数据库
        return Task.FromResult(false);
    }
}
