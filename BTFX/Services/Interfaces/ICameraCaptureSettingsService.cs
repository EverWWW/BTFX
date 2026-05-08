using BTFX.Models.Camera;

namespace BTFX.Services.Interfaces;

public interface ICameraCaptureSettingsService
{
    CameraCaptureSettings Load();

    void Save(CameraCaptureSettings settings);
}
