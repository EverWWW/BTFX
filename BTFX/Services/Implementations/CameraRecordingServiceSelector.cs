using BTFX.Models.Camera;
using BTFX.Services.Interfaces;

namespace BTFX.Services.Implementations;

public sealed class CameraRecordingServiceSelector : ICameraRecordingService
{
    private readonly FfmpegCameraRecordingService _directShowService;
    private readonly DahengCameraRecordingService _dahengService;
    private readonly ICameraCaptureSettingsService _settingsService;

    public CameraRecordingServiceSelector(
        FfmpegCameraRecordingService directShowService,
        DahengCameraRecordingService dahengService,
        ICameraCaptureSettingsService settingsService)
    {
        _directShowService = directShowService;
        _dahengService = dahengService;
        _settingsService = settingsService;
    }

    public Task<IReadOnlyList<CameraRecordingResult>> RecordAsync(
        CameraRecordingOptions options,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.Load();
        return settings.ResolveBackend() == CameraCaptureBackend.Daheng
            ? _dahengService.RecordAsync(options, logProgress, cancellationToken)
            : _directShowService.RecordAsync(options, logProgress, cancellationToken);
    }
}
