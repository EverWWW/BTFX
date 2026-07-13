using System.IO;
using System.Windows;

namespace BTFX.Helpers;

public static class DiskSpaceGuard
{
    private const long MinimumFreeBytes = 2L * 1024 * 1024 * 1024;

    public static bool EnsureProgramDriveHasSpace(string operationName)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var root = Path.GetPathRoot(baseDirectory);
        if (string.IsNullOrWhiteSpace(root))
        {
            return true;
        }

        try
        {
            var drive = new DriveInfo(root);
            if (drive.AvailableFreeSpace >= MinimumFreeBytes)
            {
                return true;
            }

            var messageTemplate = Application.Current?.TryFindResource("DiskSpace.LowMessageFormat")?.ToString()
                                  ?? "{0} needs to write video or analysis result files, but the drive has less than 2 GB of free space.";
            var title = Application.Current?.TryFindResource("DiskSpace.LowTitle")?.ToString()
                        ?? "Insufficient Disk Space";
            AppDialog.Show(
                string.Format(messageTemplate, operationName),
                title,
                AppDialogButtons.Ok,
                AppDialogIcon.Warning);
            return false;
        }
        catch
        {
            return true;
        }
    }
}
