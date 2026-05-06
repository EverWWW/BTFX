namespace BTFX.Models.Camera;

public sealed class CameraRecordingResult
{
    public CameraRecordingResult(string cameraName, string aviFile, string? mp4File)
    {
        CameraName = cameraName;
        AviFile = aviFile;
        Mp4File = mp4File;
    }

    public string CameraName { get; }

    public string AviFile { get; }

    public string? Mp4File { get; }
}
