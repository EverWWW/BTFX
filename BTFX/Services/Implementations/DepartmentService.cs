using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

/// <summary>
/// 科室服务实现（占位实现，第四阶段完善）
/// </summary>
public class DepartmentService : IDepartmentService
{
    // TODO: 注入数据库服务

    /// <inheritdoc/>
    public Task<List<Department>> GetAllDepartmentsAsync()
    {
        // TODO: 从数据库读取
        return Task.FromResult(new List<Department>());
    }

    /// <inheritdoc/>
    public Task<List<Department>> GetEnabledDepartmentsAsync()
    {
        // TODO: 从数据库读取启用的科室
        return Task.FromResult(new List<Department>());
    }

    /// <inheritdoc/>
    public Task<Department?> GetDepartmentByIdAsync(int id)
    {
        // TODO: 从数据库读取
        return Task.FromResult<Department?>(null);
    }

    /// <inheritdoc/>
    public Task<int> AddDepartmentAsync(Department department)
    {
        // TODO: 插入数据库
        return Task.FromResult(0);
    }

    /// <inheritdoc/>
    public Task<bool> UpdateDepartmentAsync(Department department)
    {
        // TODO: 更新数据库
        return Task.FromResult(false);
    }

    /// <inheritdoc/>
    public Task<bool> DeleteDepartmentAsync(int id)
    {
        // TODO: 删除数据库记录
        return Task.FromResult(false);
    }
}
