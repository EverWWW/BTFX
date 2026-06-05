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

            MessageBox.Show(
                $"{operationName}需要写入视频或分析结果文件，当前程序所在磁盘剩余空间不足 2GB。\n\n请先清理磁盘空间后再继续。",
                "磁盘空间不足",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
        catch
        {
            return true;
        }
    }
}
