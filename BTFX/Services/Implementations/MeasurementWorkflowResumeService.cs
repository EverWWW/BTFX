using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

public sealed class MeasurementWorkflowResumeService : IMeasurementWorkflowResumeService
{
    private readonly IMeasurementVideoValidationService _videoValidationService;
    private readonly IGaitAnalysisService _analysisService;

    public MeasurementWorkflowResumeService(
        IMeasurementVideoValidationService videoValidationService,
        IGaitAnalysisService analysisService)
    {
        _videoValidationService = videoValidationService;
        _analysisService = analysisService;
    }

    public async Task<MeasurementResumeDecision> DecideAsync(
        MeasurementRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var videoValidation = await _videoValidationService.ValidateAsync(record, cancellationToken);

        return record.Status switch
        {
            MeasurementStatus.Pending => videoValidation.CanContinue
                ? new MeasurementResumeDecision(true, 2, "继续处理", "已恢复到回放检查，可继续确认视频后进入分析。")
                : new MeasurementResumeDecision(true, 1, "继续处理", videoValidation.Message),

            MeasurementStatus.InProgress => _analysisService.IsAnalysisRunning
                ? new MeasurementResumeDecision(true, 3, "查看进度", "该测量正在分析中，已恢复到分析进度界面。")
                : new MeasurementResumeDecision(true, 3, "继续处理", "上次分析没有检测到后台任务，建议重新分析。", RequiresReanalysis: true),

            MeasurementStatus.Completed => new MeasurementResumeDecision(true, 3, "查看详情", "已恢复到分析结果界面。"),

            MeasurementStatus.Failed => videoValidation.CanContinue
                ? new MeasurementResumeDecision(true, 3, "重新分析", "上次分析失败，可检查配置后重新分析。", RequiresReanalysis: true)
                : new MeasurementResumeDecision(true, 1, "继续处理", videoValidation.Message, RequiresReanalysis: true),

            MeasurementStatus.Cancelled => new MeasurementResumeDecision(true, 1, "继续处理", "该测量处于待处理状态，可继续编辑测量和视频。"),

            _ => new MeasurementResumeDecision(true, 1, "继续处理", "已恢复到新建测量界面。")
        };
    }
}
