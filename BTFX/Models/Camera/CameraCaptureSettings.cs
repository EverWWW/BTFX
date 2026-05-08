namespace BTFX.Models.Camera;

public sealed class CameraCaptureSettings
{
    public string SideCameraName { get; set; } = "Y-CAM-25320046";

    public string FrontCameraName { get; set; } = "Y-CAM-24500213";

    public CameraCaptureMode LastMode { get; set; } = CameraCaptureMode.Dual;

    public string Resolution { get; set; } = "3840x2160";

    public int FrameRate { get; set; } = 59;

    public int DurationSeconds { get; set; } = 10;

    public CameraTransformOptions SideTransform { get; set; } = new();

    public CameraTransformOptions FrontTransform { get; set; } = new();
}

public enum CameraCaptureMode
{
    Single,
    Dual
}

public enum CameraViewRole
{
    Side,
    Front
}

public enum CameraOrientation
{
    Landscape,
    PortraitClockwise
}

public sealed class CameraTransformOptions
{
    public CameraOrientation Orientation { get; set; } = CameraOrientation.Landscape;

    public bool FlipHorizontal { get; set; }
}
