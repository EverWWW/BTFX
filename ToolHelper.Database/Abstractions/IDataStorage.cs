namespace ToolHelper.Database.Abstractions;

/// <summary>
/// 数据存储接口
/// 提供通用的CRUD操作
/// </summary>
/// <typeparam name="TEntity">实体类型</typeparam>
/// <typeparam name="TKey">主键类型</typeparam>
public interface IDataStorage<TEntity, TKey> where TEntity : class
{
    /// <summary>
    /// 表名
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// 根据主键获取实体
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体对象</returns>
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有实体
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体集合</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件查询
    /// </summary>
    /// <param name="whereClause">WHERE子句（不含WHERE关键字）</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>实体集合</returns>
    Task<IEnumerable<TEntity>> GetByConditionAsync(
        string whereClause,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <param name="pageIndex">页索引（从0开始）</param>
    /// <param name="pageSize">页大小</param>
    /// <param name="orderBy">排序字段</param>
    /// <param name="whereClause">WHERE子句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页结果</returns>
    Task<PagedResult<TEntity>> GetPagedAsync(
        int pageIndex,
        int pageSize,
        string? orderBy = null,
        string? whereClause = null,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 插入实体
    /// </summary>
    /// <param name="entity">实体对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>插入后的实体（含自增ID）</returns>
    Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量插入
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>插入的行数</returns>
    Task<int> InsertRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <param name="entity">实体对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量更新
    /// </summary>
    /// <param name="entities">实体集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>更新的行数</returns>
    Task<int> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否成功</returns>
    Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量删除
    /// </summary>
    /// <param name="ids">主键集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除的行数</returns>
    Task<int> DeleteRangeAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件删除
    /// </summary>
    /// <param name="whereClause">WHERE子句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>删除的行数</returns>
    Task<int> DeleteByConditionAsync(
        string whereClause,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 统计记录数
    /// </summary>
    /// <param name="whereClause">WHERE子句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>记录数</returns>
    Task<long> CountAsync(
        string? whereClause = null,
        object? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查是否存在
    /// </summary>
    /// <param name="id">主键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据条件检查是否存在
    /// </summary>
    /// <param name="whereClause">WHERE子句</param>
    /// <param name="parameters">参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>是否存在</returns>
    Task<bool> ExistsByConditionAsync(
        string whereClause,
        object? parameters = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 分页结果
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public record PagedResult<T>
{
    /// <summary>数据集合</summary>
    public IReadOnlyList<T> Items { get; init; } = [];
    
    /// <summary>当前页索引</summary>
    public int PageIndex { get; init; }
    
    /// <summary>页大小</summary>
    public int PageSize { get; init; }
    
    /// <summary>总记录数</summary>
    public long TotalCount { get; init; }
    
    /// <summary>总页数</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    
    /// <summary>是否有上一页</summary>
    public bool HasPrevious => PageIndex > 0;
    
    /// <summary>是否有下一页</summary>
    public bool HasNext => PageIndex < TotalPages - 1;
}
