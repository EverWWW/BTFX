using System.IO;
using BTFX.Models;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

public sealed class MeasurementVideoValidationService : IMeasurementVideoValidationService
{
    public Task<MeasurementVideoValidationResult> ValidateAsync(
        MeasurementRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var videoPaths = new[]
            {
                ("侧面视频", record.SideVideoPath),
                ("正面视频", record.FrontVideoPath)
            }
            .Where(item => !string.IsNullOrWhiteSpace(item.Item2))
            .ToList();

        if (videoPaths.Count == 0)
        {
            return Task.FromResult(new MeasurementVideoValidationResult(false, false, "该测量还没有导入或采集视频，请先补充视频。"));
        }

        foreach (var (name, path) in videoPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(path))
            {
                return Task.FromResult(new MeasurementVideoValidationResult(true, false, $"{name}文件不存在：{path}"));
            }

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length <= 0)
            {
                return Task.FromResult(new MeasurementVideoValidationResult(true, false, $"{name}文件为空，请重新选择视频。"));
            }
        }

        return Task.FromResult(new MeasurementVideoValidationResult(true, true, "视频文件可用。"));
    }
}
