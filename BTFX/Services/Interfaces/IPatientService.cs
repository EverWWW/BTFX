using BTFX.Models;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 患者服务接口
/// </summary>
public interface IPatientService
{
    /// <summary>
    /// 获取所有患者列表
    /// </summary>
    /// <returns>患者列表</returns>
    Task<List<Patient>> GetAllPatientsAsync();

    /// <summary>
    /// 分页获取患者列表
    /// </summary>
    /// <param name="pageIndex">页码（从0开始）</param>
    /// <param name="pageSize">每页数量</param>
    /// <param name="searchText">搜索文本（可选）</param>
    /// <returns>患者列表和总数</returns>
    Task<(List<Patient> Patients, int TotalCount)> GetPatientsPagedAsync(int pageIndex, int pageSize, string? searchText = null);

    /// <summary>
    /// 根据ID获取患者
    /// </summary>
    /// <param name="id">患者ID</param>
    /// <returns>患者信息</returns>
    Task<Patient?> GetPatientByIdAsync(int id);

    /// <summary>
    /// 添加患者
    /// </summary>
    /// <param name="patient">患者信息</param>
    /// <returns>新增的患者ID</returns>
    Task<int> AddPatientAsync(Patient patient);

    /// <summary>
    /// 更新患者信息
    /// </summary>
    /// <param name="patient">患者信息</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdatePatientAsync(Patient patient);

    /// <summary>
    /// 删除患者（逻辑删除）
    /// </summary>
    /// <param name="id">患者ID</param>
    /// <returns>是否成功</returns>
    Task<bool> DeletePatientAsync(int id);

    /// <summary>
    /// 搜索患者
    /// </summary>
    /// <param name="searchText">搜索文本</param>
    /// <returns>患者列表</returns>
    Task<List<Patient>> SearchPatientsAsync(string searchText);

    /// <summary>
    /// 检查患者是否存在
    /// </summary>
    /// <param name="phone">电话</param>
    /// <param name="excludeId">排除的患者ID（用于更新时检查）</param>
    /// <returns>是否存在</returns>
    Task<bool> IsPatientExistsAsync(string phone, int? excludeId = null);
}
