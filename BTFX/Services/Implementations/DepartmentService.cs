using BTFX.Data;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 科室服务实现（使用 SqlSugar）
/// </summary>
public class DepartmentService : IDepartmentService
{
    private readonly ILogHelper? _logHelper;

    /// <summary>
    /// 构造函数
    /// </summary>
    public DepartmentService()
    {
        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }
    }

    /// <inheritdoc/>
    public async Task<List<Department>> GetAllDepartmentsAsync()
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            return await db.GetListAsync<Department>(d => true);
        }
        catch (Exception ex)
        {
            _logHelper?.Error("获取科室列表失败", ex);
            return new List<Department>();
        }
    }

    /// <inheritdoc/>
    public async Task<Department?> GetDepartmentByIdAsync(int id)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            return await db.GetByIdAsync<Department>(id);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"获取科室失败: Id={id}", ex);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<int> AddDepartmentAsync(Department department)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            var now = DateTime.Now;
            department.CreatedAt = now;
            department.UpdatedAt = now;

            var id = await db.InsertReturnIdentityAsync(department);

            _logHelper?.Information($"添加科室成功: Id={id}, Name={department.Name}");
            return (int)id;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"添加科室失败: Name={department.Name}", ex);
            return 0;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateDepartmentAsync(Department department)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            department.UpdatedAt = DateTime.Now;

            var success = await db.UpdateAsync(department);

            if (success)
            {
                _logHelper?.Information($"更新科室成功: Id={department.Id}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"更新科室失败: Id={department.Id}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteDepartmentAsync(int id)
    {
        try
        {
            // 先检查是否被使用
            if (await IsDepartmentInUseAsync(id))
            {
                _logHelper?.Warning($"科室被使用，无法删除: Id={id}");
                return false;
            }

            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            var success = await db.DeleteByIdAsync<Department>(id);

            if (success)
            {
                _logHelper?.Information($"删除科室成功: Id={id}");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除科室失败: Id={id}", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsDepartmentInUseAsync(int id)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();
            return await db.AnyAsync<User>(u => u.DepartmentId == id);
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"检查科室使用失败: Id={id}", ex);
            return true; // 出错时返回 true，防止误删
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CheckNameExistsAsync(string name, int? excludeId = null)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteSugarHelper();

            if (excludeId.HasValue)
            {
                return await db.AnyAsync<Department>(d => d.Name == name && d.Id != excludeId.Value);
            }
            else
            {
                return await db.AnyAsync<Department>(d => d.Name == name);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"检查名称重复失败: Name={name}", ex);
            return true; // 出错时返回 true，防止重复添加
        }
    }
}
