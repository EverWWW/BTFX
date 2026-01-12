using BTFX.Models;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 科室服务接口
/// </summary>
public interface IDepartmentService
{
    /// <summary>
    /// 获取所有科室
    /// </summary>
    /// <returns>科室列表</returns>
    Task<List<Department>> GetAllDepartmentsAsync();

    /// <summary>
    /// 根据ID获取科室
    /// </summary>
    /// <param name="id">科室ID</param>
    /// <returns>科室信息</returns>
    Task<Department?> GetDepartmentByIdAsync(int id);

    /// <summary>
    /// 添加科室
    /// </summary>
    /// <param name="department">科室信息</param>
    /// <returns>新增的科室ID</returns>
    Task<int> AddDepartmentAsync(Department department);

    /// <summary>
    /// 更新科室
    /// </summary>
    /// <param name="department">科室信息</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateDepartmentAsync(Department department);

    /// <summary>
    /// 删除科室
    /// </summary>
    /// <param name="id">科室ID</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteDepartmentAsync(int id);

    /// <summary>
    /// 检查科室是否被用户引用
    /// </summary>
    /// <param name="id">科室ID</param>
    /// <returns>是否被引用</returns>
    Task<bool> IsDepartmentInUseAsync(int id);

    /// <summary>
    /// 检查科室名称是否已存在
    /// </summary>
    /// <param name="name">科室名称</param>
    /// <param name="excludeId">排除的科室ID（编辑时使用）</param>
    /// <returns>是否已存在</returns>
    Task<bool> CheckNameExistsAsync(string name, int? excludeId = null);
}
