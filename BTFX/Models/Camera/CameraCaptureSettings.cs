namespace BTFX.Models.Camera;

public sealed class CameraCaptureSettings
{
    public const string DeviceTypeYunxi = "YUNXI";

    public const string DeviceTypeDaheng = "DAHENG";

    public string DeviceType { get; set; } = DeviceTypeYunxi;

    public CameraCaptureBackend Backend { get; set; } = CameraCaptureBackend.DirectShow;

    public string SideCameraName { get; set; } = "Y-CAM-25320046";

    public string FrontCameraName { get; set; } = "Y-CAM-24500213";

    public string DahengSideCameraSerialNumber { get; set; } = string.Empty;

    public string DahengFrontCameraSerialNumber { get; set; } = string.Empty;

    public CameraCaptureMode LastMode { get; set; } = CameraCaptureMode.Dual;

    public string Resolution { get; set; } = "3840x2160";

    public int FrameRate { get; set; } = 60;

    public int DurationSeconds { get; set; } = 10;

    public string ExternalConfigToolPath { get; set; } = string.Empty;

    public CameraTransformOptions SideTransform { get; set; } = new();

    public CameraTransformOptions FrontTransform { get; set; } = new();

    public CameraCaptureBackend ResolveBackend()
    {
        if (string.Equals(DeviceType, DeviceTypeDaheng, StringComparison.OrdinalIgnoreCase))
        {
            return CameraCaptureBackend.Daheng;
        }

        if (string.Equals(DeviceType, DeviceTypeYunxi, StringComparison.OrdinalIgnoreCase))
        {
            return CameraCaptureBackend.DirectShow;
        }

        return Backend;
    }

    public void NormalizeDeviceType()
    {
        DeviceType = ResolveBackend() == CameraCaptureBackend.Daheng ? DeviceTypeDaheng : DeviceTypeYunxi;
        Backend = ResolveBackend();
    }
}

public enum CameraCaptureBackend
{
    DirectShow,
    Daheng
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
