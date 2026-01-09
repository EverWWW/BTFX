using System.Linq.Expressions;
using SqlSugar;

namespace ToolHelper.Database.Abstractions;

/// <summary>
/// SqlSugar 数据库帮助类接口
/// 提供基于 ORM 的数据库操作，无需编写 SQL 语句
/// </summary>
public interface ISqlSugarDbHelper : IDisposable
{
    /// <summary>
    /// 获取 SqlSugar 客户端实例
    /// </summary>
    ISqlSugarClient Db { get; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    DbType DatabaseType { get; }

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }

    #region 连接管理

    /// <summary>
    /// 测试数据库连接
    /// </summary>
    /// <returns>连接是否成功</returns>
    bool TestConnection();

    /// <summary>
    /// 异步测试数据库连接
    /// </summary>
    Task<bool> TestConnectionAsync();

    #endregion

    #region 查询操作

    /// <summary>
    /// 根据主键获取实体
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="id">主键值</param>
    /// <returns>实体对象</returns>
    T? GetById<T>(object id) where T : class, new();

    /// <summary>
    /// 异步根据主键获取实体
    /// </summary>
    Task<T?> GetByIdAsync<T>(object id) where T : class, new();

    /// <summary>
    /// 获取所有数据
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <returns>实体集合</returns>
    List<T> GetAll<T>() where T : class, new();

    /// <summary>
    /// 异步获取所有数据
    /// </summary>
    Task<List<T>> GetAllAsync<T>() where T : class, new();

    /// <summary>
    /// 根据条件查询
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="whereExpression">条件表达式</param>
    /// <returns>实体集合</returns>
    List<T> GetList<T>(Expression<Func<T, bool>> whereExpression) where T : class, new();

    /// <summary>
    /// 异步根据条件查询
    /// </summary>
    Task<List<T>> GetListAsync<T>(Expression<Func<T, bool>> whereExpression) where T : class, new();

    /// <summary>
    /// 获取单条记录
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="whereExpression">条件表达式</param>
    /// <returns>实体对象</returns>
    T? GetFirst<T>(Expression<Func<T, bool>> whereExpression) where T : class, new();

    /// <summary>
    /// 异步获取单条记录
    /// </summary>
    Task<T?> GetFirstAsync<T>(Expression<Func<T, bool>> whereExpression) where T : class, new();

    /// <summary>
    /// 分页查询
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="whereExpression">条件表达式</param>
    /// <param name="pageIndex">页码（从1开始）</param>
    /// <param name="pageSize">每页大小</param>
    /// <param name="totalCount">总记录数</param>
    /// <param name="orderByExpression">排序表达式</param>
    /// <param name="isAsc">是否升序</param>
    /// <returns>分页数据</returns>
    List<T> GetPageList<T>(
        Expression<Func<T, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        ref int totalCount,
        Expression<Func<T, object>>? orderByExpression = null,
        bool isAsc = true) where T : class, new();

    /// <summary>
    /// 异步分页查询
    /// </summary>
    Task<DbPagedResult<T>> GetPageListAsync<T>(
        Expression<Func<T, bool>>? whereExpression,
        int pageIndex,
        int pageSize,
        Expression<Func<T, object>>? orderByExpression = null,
        bool isAsc = true) where T : class, new();

    /// <summary>
    /// 判断是否存在
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="whereExpression">条件表达式</param>
    /// <returns>是否存在</returns>
    bool Any<T>(Expression<Func<T, bool>> whereExpression) where T : class, new();

    /// <summary>
    /// 异步判断是否存在
    /// </summary>
    Task<bool> AnyAsync<T>(Expression<Func<T, bool>> whereExpression) where T : class, new();

    /// <summary>
    /// 获取记录数
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="whereExpression">条件表达式</param>
    /// <returns>记录数</returns>
    int Count<T>(Expression<Func<T, bool>>? whereExpression = null) where T : class, new();

    /// <summary>
    /// 异步获取记录数
    /// </summary>
    Task<int> CountAsync<T>(Expression<Func<T, bool>>? whereExpression = null) where T : class, new();

    #endregion

    #region 插入操作

    /// <summary>
    /// 插入实体
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <returns>是否成功</returns>
    bool Insert<T>(T entity) where T : class, new();

    /// <summary>
    /// 异步插入实体
    /// </summary>
    Task<bool> InsertAsync<T>(T entity) where T : class, new();

    /// <summary>
    /// 插入并返回自增ID
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <returns>自增ID</returns>
    int InsertReturnIdentity<T>(T entity) where T : class, new();

    /// <summary>
    /// 异步插入并返回自增ID
    /// </summary>
    Task<long> InsertReturnIdentityAsync<T>(T entity) where T : class, new();

    /// <summary>
    /// 插入并返回实体（含自增ID）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <returns>包含ID的实体</returns>
    T InsertReturnEntity<T>(T entity) where T : class, new();

    /// <summary>
    /// 异步插入并返回实体
    /// </summary>
    Task<T> InsertReturnEntityAsync<T>(T entity) where T : class, new();

    /// <summary>
    /// 批量插入
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体集合</param>
    /// <returns>插入行数</returns>
    int InsertRange<T>(List<T> entities) where T : class, new();

    /// <summary>
    /// 异步批量插入
    /// </summary>
    Task<int> InsertRangeAsync<T>(List<T> entities) where T : class, new();

    #endregion

    #region 更新操作

    /// <summary>
    /// 更新实体
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <returns>是否成功</returns>
    bool Update<T>(T entity) where T : class, new();

    /// <summary>
    /// 异步更新实体
    /// </summary>
    Task<bool> UpdateAsync<T>(T entity) where T : class, new();

    /// <summary>
    /// 批量更新
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体集合</param>
    /// <returns>更新行数</returns>
    int UpdateRange<T>(List<T> entities) where T : class, new();

    /// <summary>
    /// 异步批量更新
    /// </summary>
    Task<int> UpdateRangeAsync<T>(List<T> entities) where T : class, new();

    /// <summary>
    /// 根据条件更新指定列
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="columns">要更新的列</param>
    /// <param name="whereExpression">条件表达式</param>
    /// <returns>更新行数</returns>
    int Update<T>(
        Expression<Func<T, T>> columns,
        Expression<Func<T, bool>> whereExpression) where T : class, new();

    /// <summary>
    /// 异步根据条件更新指定列
    /// </summary>
    Task<int> UpdateAsync<T>(
        Expression<Func<T, T>> columns,
        Expression<Func<T, bool>> whereExpression) where T : class, new();

    #endregion

    #region 删除操作

    /// <summary>
    /// 根据主键删除
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="id">主键值</param>
    /// <returns>是否成功</returns>
    bool DeleteById<T>(object id) where T : class, new();

    /// <summary>
    /// 异步根据主键删除
    /// </summary>
    Task<bool> DeleteByIdAsync<T>(object id) where T : class, new();

    /// <summary>
    /// 删除实体
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <returns>是否成功</returns>
    bool Delete<T>(T entity) where T : class, new();

    /// <summary>
    /// 异步删除实体
    /// </summary>
    Task<bool> DeleteAsync<T>(T entity) where T : class, new();

    /// <summary>
    /// 根据条件删除
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="whereExpression">条件表达式</param>
    /// <returns>删除行数</returns>
    int Delete<T>(Expression<Func<T, bool>> whereExpression) where T : class, new();

    /// <summary>
    /// 异步根据条件删除
    /// </summary>
    Task<int> DeleteAsync<T>(Expression<Func<T, bool>> whereExpression) where T : class, new();

    /// <summary>
    /// 批量删除
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="ids">主键集合</param>
    /// <returns>删除行数</returns>
    int DeleteByIds<T>(object[] ids) where T : class, new();

    /// <summary>
    /// 异步批量删除
    /// </summary>
    Task<int> DeleteByIdsAsync<T>(object[] ids) where T : class, new();

    #endregion

    #region 事务操作

    /// <summary>
    /// 开始事务
    /// </summary>
    void BeginTran();

    /// <summary>
    /// 提交事务
    /// </summary>
    void CommitTran();

    /// <summary>
    /// 回滚事务
    /// </summary>
    void RollbackTran();

    /// <summary>
    /// 在事务中执行操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <returns>是否成功</returns>
    bool ExecuteInTransaction(Action action);

    /// <summary>
    /// 在事务中执行操作并返回结果
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="func">要执行的操作</param>
    /// <returns>操作结果</returns>
    T ExecuteInTransaction<T>(Func<T> func);

    /// <summary>
    /// 异步在事务中执行操作
    /// </summary>
    Task<bool> ExecuteInTransactionAsync(Func<Task> action);

    #endregion

    #region 表操作

    /// <summary>
    /// 创建表（如果不存在）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    void CreateTable<T>() where T : class, new();

    /// <summary>
    /// 创建多个表
    /// </summary>
    /// <param name="types">实体类型数组</param>
    void CreateTables(params Type[] types);

    /// <summary>
    /// 删除表
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    void DropTable<T>() where T : class, new();

    /// <summary>
    /// 清空表
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    void TruncateTable<T>() where T : class, new();

    /// <summary>
    /// 判断表是否存在
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <returns>是否存在</returns>
    bool TableExists<T>() where T : class, new();

    /// <summary>
    /// 判断表是否存在
    /// </summary>
    /// <param name="tableName">表名</param>
    /// <returns>是否存在</returns>
    bool TableExists(string tableName);

    #endregion

    #region 原生SQL

    /// <summary>
    /// 执行原生SQL（增删改）
    /// </summary>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <returns>受影响的行数</returns>
    int ExecuteSql(string sql, object? parameters = null);

    /// <summary>
    /// 异步执行原生SQL
    /// </summary>
    Task<int> ExecuteSqlAsync(string sql, object? parameters = null);

    /// <summary>
    /// 执行原生SQL查询
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <returns>查询结果</returns>
    List<T> SqlQuery<T>(string sql, object? parameters = null) where T : class, new();

    /// <summary>
    /// 异步执行原生SQL查询
    /// </summary>
    Task<List<T>> SqlQueryAsync<T>(string sql, object? parameters = null) where T : class, new();

    /// <summary>
    /// 执行标量查询
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="sql">SQL语句</param>
    /// <param name="parameters">参数</param>
    /// <returns>标量值</returns>
    T? SqlQueryScalar<T>(string sql, object? parameters = null);

    /// <summary>
    /// 异步执行标量查询
    /// </summary>
    Task<T?> SqlQueryScalarAsync<T>(string sql, object? parameters = null);

    #endregion

    #region 高级查询

    /// <summary>
    /// 获取可查询对象（支持链式调用）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <returns>可查询对象</returns>
    ISugarQueryable<T> Queryable<T>() where T : class, new();

    /// <summary>
    /// 获取可插入对象
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <returns>可插入对象</returns>
    IInsertable<T> Insertable<T>(T entity) where T : class, new();

    /// <summary>
    /// 获取可插入对象（批量）
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entities">实体集合</param>
    /// <returns>可插入对象</returns>
    IInsertable<T> Insertable<T>(List<T> entities) where T : class, new();

    /// <summary>
    /// 获取可更新对象
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <param name="entity">实体对象</param>
    /// <returns>可更新对象</returns>
    IUpdateable<T> Updateable<T>(T entity) where T : class, new();

    /// <summary>
    /// 获取可删除对象
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <returns>可删除对象</returns>
    IDeleteable<T> Deleteable<T>() where T : class, new();

    #endregion
}

/// <summary>
/// 分页结果
/// </summary>
/// <typeparam name="T">实体类型</typeparam>
public class DbPagedResult<T>
{
    /// <summary>数据集合</summary>
    public List<T> Items { get; set; } = [];

    /// <summary>当前页码</summary>
    public int PageIndex { get; set; }

    /// <summary>每页大小</summary>
    public int PageSize { get; set; }

    /// <summary>总记录数</summary>
    public int TotalCount { get; set; }

    /// <summary>总页数</summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;

    /// <summary>是否有上一页</summary>
    public bool HasPrevious => PageIndex > 1;

    /// <summary>是否有下一页</summary>
    public bool HasNext => PageIndex < TotalPages;
}
