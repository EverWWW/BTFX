using System.Data.Common;

namespace ToolHelper.Database.Abstractions;

/// <summary>
/// 数据库连接工厂接口
/// 用于创建和管理数据库连接
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// 数据库类型
    /// </summary>
    DatabaseType DatabaseType { get; }

    /// <summary>
    /// 创建新连接
    /// </summary>
    /// <returns>数据库连接</returns>
    DbConnection CreateConnection();

    /// <summary>
    /// 创建并打开连接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>已打开的数据库连接</returns>
    Task<DbConnection> CreateAndOpenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 数据库命令参数
/// </summary>
public class DbParameter
{
    /// <summary>参数名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>参数值</summary>
    public object? Value { get; set; }

    /// <summary>参数类型</summary>
    public Type? ParameterType { get; set; }

    /// <summary>
    /// 创建参数
    /// </summary>
    /// <param name="name">参数名</param>
    /// <param name="value">参数值</param>
    public DbParameter(string name, object? value)
    {
        Name = name;
        Value = value;
        ParameterType = value?.GetType();
    }
}

/// <summary>
/// 查询结果映射器接口
/// </summary>
/// <typeparam name="T">目标类型</typeparam>
public interface IResultMapper<T>
{
    /// <summary>
    /// 从数据读取器映射到实体
    /// </summary>
    /// <param name="reader">数据读取器</param>
    /// <returns>实体对象</returns>
    T Map(DbDataReader reader);
}
