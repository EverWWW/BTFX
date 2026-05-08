namespace BTFX.Models.Camera;

public sealed class CameraRecordingOptions
{
    public string FfmpegPath { get; set; } = @"D:\ffmpeg\bin\ffmpeg.exe";

    public string SaveDirectory { get; set; } = @"D:\ffmpeg\video";

    public IReadOnlyList<string> CameraNames { get; set; } = Array.Empty<string>();

    public string VideoSize { get; set; } = "3840x2160";

    public int FrameRate { get; set; } = 59;

    public int DurationSeconds { get; set; } = 6;

    public bool TranscodeToMp4 { get; set; } = true;

    public bool DeleteAviAfterMp4 { get; set; } = true;

    public IReadOnlyDictionary<string, CameraTransformOptions> TransformOptionsByCameraName { get; set; } =
        new Dictionary<string, CameraTransformOptions>(StringComparer.OrdinalIgnoreCase);
}
