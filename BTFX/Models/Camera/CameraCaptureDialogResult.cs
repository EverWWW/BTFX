namespace BTFX.Models.Camera;

public sealed class CameraCaptureDialogResult
{
    public CameraCaptureMode Mode { get; init; }

    public string? SideVideoPath { get; init; }

    public string? FrontVideoPath { get; init; }

    public string? SideCameraName { get; init; }

    public string? FrontCameraName { get; init; }
}
