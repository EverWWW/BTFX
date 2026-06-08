using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Xml;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 参考通用框架的在线更新服务。
/// </summary>
public class AppUpdateService : IAppUpdateService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogHelper? _logHelper;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public AppUpdateService(ISettingsService settingsService, ILogHelper? logHelper = null)
    {
        _settingsService = settingsService;
        _logHelper = logHelper;
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var updateInfo = await CheckForUpdatesAsync(false, cancellationToken);
        if (updateInfo is null)
        {
            return;
        }

        ShowUpdatePrompt(updateInfo);
    }

    public async Task<AppUpdateInfo?> CheckForUpdatesAsync(bool force, CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.CurrentSettings.Update;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.UpdateUrl))
        {
            return null;
        }

        if (!force
            && DateTime.TryParse(settings.LastCheckDate, out var lastCheck)
            && lastCheck.Date.AddDays(Math.Max(1, settings.CheckIntervalDays)) > DateTime.Now.Date)
        {
            return null;
        }

        try
        {
            var xmlText = await _httpClient.GetStringAsync(settings.UpdateUrl, cancellationToken);
            var xml = new XmlDocument();
            xml.LoadXml(xmlText);

            var node = xml.SelectSingleNode($"update/{Constants.APP_NAME}");
            if (node?.Attributes?["version"] == null)
            {
                SaveLastCheckDate(settings);
                return null;
            }

            var latestVersionText = node.Attributes["version"]!.Value;
            var latestVersion = new Version(latestVersionText);
            var currentVersion = new Version(Constants.VERSION_FULL.TrimStart('V', 'v'));
            if (latestVersion <= currentVersion)
            {
                SaveLastCheckDate(settings);
                return null;
            }

            SaveLastCheckDate(settings);

            var packageUrl = ResolvePackageUrl(settings.UpdateUrl, node.Attributes["url"]?.Value ?? string.Empty);
            var detail = node.InnerText?.Trim() ?? string.Empty;
            return new AppUpdateInfo(latestVersionText, packageUrl, detail);
        }
        catch (Exception ex)
        {
            _logHelper?.Warning($"检查在线更新失败：{ex.Message}");
            return null;
        }
    }

    public async Task<string> DownloadUpdatePackageAsync(
        AppUpdateInfo updateInfo,
        IProgress<OperationProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(updateInfo.PackageUrl))
        {
            throw new InvalidOperationException("更新包地址为空。");
        }

        progress?.Report(new OperationProgressInfo(5, "准备下载", "正在连接更新服务器..."));

        var downloadDirectory = Path.Combine(Path.GetTempPath(), Constants.APP_NAME, "Updates");
        Directory.CreateDirectory(downloadDirectory);

        var localPath = Path.Combine(downloadDirectory, GetPackageFileName(updateInfo.PackageUrl, updateInfo.Version));
        if (File.Exists(localPath))
        {
            File.Delete(localPath);
        }

        using var response = await _httpClient.GetAsync(updateInfo.PackageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(localPath);

        var buffer = new byte[1024 * 128];
        long readBytes = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            readBytes += read;

            if (totalBytes is > 0)
            {
                var percent = 10 + readBytes * 80.0 / totalBytes.Value;
                progress?.Report(new OperationProgressInfo(percent, "正在下载", $"已下载 {FormatBytes(readBytes)} / {FormatBytes(totalBytes.Value)}"));
            }
            else
            {
                progress?.Report(new OperationProgressInfo(50, "正在下载", $"已下载 {FormatBytes(readBytes)}", true));
            }
        }

        progress?.Report(new OperationProgressInfo(95, "下载完成", "正在校验更新包..."));
        if (!File.Exists(localPath) || new FileInfo(localPath).Length == 0)
        {
            throw new InvalidOperationException("更新包下载失败或文件为空。");
        }

        progress?.Report(new OperationProgressInfo(100, "下载完成", $"更新包已保存到 {localPath}"));
        return localPath;
    }

    public void StartInstallerAndShutdown(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("更新安装包不存在。", installerPath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        });

        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
    }

    private void SaveLastCheckDate(UpdateSettings settings)
    {
        settings.LastCheckDate = DateTime.Now.ToString("yyyy-MM-dd");
        _settingsService.SaveSettings();
    }

    private static void ShowUpdatePrompt(AppUpdateInfo updateInfo)
    {
        var message = string.IsNullOrWhiteSpace(updateInfo.Detail)
            ? $"发现新版本 {updateInfo.Version}。\n\n更新包：{updateInfo.PackageUrl}"
            : $"发现新版本 {updateInfo.Version}。\n\n更新内容：\n{updateInfo.Detail}\n\n更新包：{updateInfo.PackageUrl}";

        Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(message, "发现新版本", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private static string ResolvePackageUrl(string updateXmlUrl, string packageUrl)
    {
        if (string.IsNullOrWhiteSpace(packageUrl))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(packageUrl, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        return Uri.TryCreate(updateXmlUrl, UriKind.Absolute, out var baseUri)
            && Uri.TryCreate(baseUri, packageUrl, out var combined)
            ? combined.ToString()
            : packageUrl;
    }

    private static string GetPackageFileName(string packageUrl, string version)
    {
        var fileName = $"BTFX_Setup_{version}.exe";
        if (Uri.TryCreate(packageUrl, UriKind.Absolute, out var uri))
        {
            var uriFileName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(uriFileName))
            {
                fileName = uriFileName;
            }
        }

        return fileName;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:F1} {units[unitIndex]}";
    }
}
