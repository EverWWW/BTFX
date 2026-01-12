using BTFX.Common;
using BTFX.Data;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 科室服务实现
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var departments = await db.QueryAsync<Department>(@"
                SELECT Id, Name, Phone, CreatedAt, UpdatedAt 
                FROM Departments 
                ORDER BY Id
            ");

            return departments.ToList();
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            return await db.QueryFirstOrDefaultAsync<Department>(@"
                SELECT Id, Name, Phone, CreatedAt, UpdatedAt 
                FROM Departments 
                WHERE Id = @Id
            ", new { Id = id });
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);

            var id = await db.InsertAndGetIdAsync(@"
                INSERT INTO Departments (Name, Phone, CreatedAt, UpdatedAt)
                VALUES (@Name, @Phone, @CreatedAt, @UpdatedAt)
            ", new
            {
                department.Name,
                Phone = department.Phone ?? "",
                CreatedAt = now,
                UpdatedAt = now
            });

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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var now = DateTime.Now.ToString(Constants.DATETIME_FORMAT);

            var affected = await db.ExecuteNonQueryAsync(@"
                UPDATE Departments 
                SET Name = @Name, Phone = @Phone, UpdatedAt = @UpdatedAt
                WHERE Id = @Id
            ", new
            {
                department.Id,
                department.Name,
                Phone = department.Phone ?? "",
                UpdatedAt = now
            });

            if (affected > 0)
            {
                _logHelper?.Information($"更新科室成功: Id={department.Id}");
            }

            return affected > 0;
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
            // 先检查是否被引用
            if (await IsDepartmentInUseAsync(id))
            {
                _logHelper?.Warning($"科室被引用，无法删除: Id={id}");
                return false;
            }

            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var affected = await db.ExecuteNonQueryAsync(@"
                DELETE FROM Departments WHERE Id = @Id
            ", new { Id = id });

            if (affected > 0)
            {
                _logHelper?.Information($"删除科室成功: Id={id}");
            }

            return affected > 0;
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
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            var count = await db.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM Users WHERE DepartmentId = @Id
            ", new { Id = id });

            return count > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"检查科室引用失败: Id={id}", ex);
            return true; // 出错时返回 true，防止误删
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CheckNameExistsAsync(string name, int? excludeId = null)
    {
        try
        {
            using var db = DatabaseFactory.CreateSqliteHelper();
            await db.InitializeAsync();

            string sql;
            object parameters;

            if (excludeId.HasValue)
            {
                sql = "SELECT COUNT(*) FROM Departments WHERE Name = @Name AND Id != @ExcludeId";
                parameters = new { Name = name, ExcludeId = excludeId.Value };
            }
            else
            {
                sql = "SELECT COUNT(*) FROM Departments WHERE Name = @Name";
                parameters = new { Name = name };
            }

            var count = await db.ExecuteScalarAsync<int>(sql, parameters);
            return count > 0;
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"检查科室名称失败: Name={name}", ex);
            return true; // 出错时返回 true，防止重复添加
        }
    }
}
