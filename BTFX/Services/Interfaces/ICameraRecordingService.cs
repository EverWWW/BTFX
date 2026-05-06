using BTFX.Models.Camera;

namespace BTFX.Services.Interfaces;

public interface ICameraRecordingService
{
    Task<IReadOnlyList<CameraRecordingResult>> RecordAsync(
        CameraRecordingOptions options,
        IProgress<string>? logProgress = null,
        CancellationToken cancellationToken = default);
}
