using BTFX.Models.Analysis;

namespace BTFX.Services.Interfaces;

/// <summary>
/// 步态分析算法调用服务接口
/// </summary>
public interface IGaitAnalysisService
{
    /// <summary>
    /// 执行完整分析流程，返回分析结果
    /// </summary>
    /// <param name="request">分析请求参数</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>分析结果</returns>
    Task<AnalysisResult> RunAnalysisAsync(AnalysisRequest request, CancellationToken ct = default);

    /// <summary>
    /// 取消当前正在运行的分析任务
    /// </summary>
    Task CancelCurrentAnalysisAsync();

    /// <summary>
    /// 当前是否有分析任务在运行
    /// </summary>
    bool IsAnalysisRunning { get; }

    /// <summary>
    /// 验证算法运行环境（exe 是否存在等）
    /// </summary>
    /// <returns>环境是否就绪</returns>
    Task<bool> ValidateEnvironmentAsync();

    /// <summary>
    /// 保存分析结果到数据库（主表 + 子表，事务写入）
    /// </summary>
    /// <param name="result">分析结果（含导航属性 KinematicSummary/CsvFiles/QualityControl）</param>
    /// <returns>保存后的分析结果 ID，失败返回 0</returns>
    Task<int> SaveAnalysisResultAsync(AnalysisResult result);

    /// <summary>
    /// 按测量记录 ID 获取最新成功的分析结果（含子表）
    /// </summary>
    /// <param name="measurementId">测量记录 ID</param>
    /// <returns>分析结果，不存在返回 null</returns>
    Task<AnalysisResult?> GetLatestAnalysisResultAsync(int measurementId);

    /// <summary>
    /// 按分析结果 ID 获取完整结果（含子表）
    /// </summary>
    /// <param name="analysisResultId">分析结果 ID</param>
    /// <returns>分析结果，不存在返回 null</returns>
    Task<AnalysisResult?> GetAnalysisResultByIdAsync(int analysisResultId);

    /// <summary>
    /// 进度变更事件
    /// </summary>
    event EventHandler<AnalysisProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// 日志消息事件
    /// </summary>
    event EventHandler<AnalysisLogEventArgs>? LogReceived;
}
