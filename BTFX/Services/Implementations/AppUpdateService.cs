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
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public AppUpdateService(
        ISettingsService settingsService,
        ILocalizationService localizationService,
        ILogHelper? logHelper = null)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
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
            _logHelper?.Warning($"Check online update failed: {ex.Message}");
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
            throw new InvalidOperationException(L("Update.PackageUrlRequired"));
        }

        progress?.Report(new OperationProgressInfo(5, L("Update.PreparingDownload"), L("Update.ConnectingServer")));

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
                progress?.Report(new OperationProgressInfo(percent, L("Update.Downloading"), L("Update.DownloadedFormat", FormatBytes(readBytes), FormatBytes(totalBytes.Value))));
            }
            else
            {
                progress?.Report(new OperationProgressInfo(50, L("Update.Downloading"), L("Update.DownloadedUnknownTotalFormat", FormatBytes(readBytes)), true));
            }
        }

        progress?.Report(new OperationProgressInfo(95, L("Update.DownloadCompleted"), L("Update.VerifyingPackage")));
        if (!File.Exists(localPath) || new FileInfo(localPath).Length == 0)
        {
            throw new InvalidOperationException(L("Update.PackageEmpty"));
        }

        progress?.Report(new OperationProgressInfo(100, L("Update.DownloadCompleted"), L("Update.PackageSavedFormat", localPath)));
        return localPath;
    }

    public void StartInstallerAndShutdown(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException(L("Update.InstallerMissing"), installerPath);
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

    private void ShowUpdatePrompt(AppUpdateInfo updateInfo)
    {
        var message = string.IsNullOrWhiteSpace(updateInfo.Detail)
            ? L("Update.AutoFoundMessageFormat", updateInfo.Version, updateInfo.PackageUrl)
            : L("Update.AutoFoundMessageWithDetailFormat", updateInfo.Version, updateInfo.Detail, updateInfo.PackageUrl);

        Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(message, L("Update.FoundTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private string L(string key)
    {
        var value = _localizationService.GetString(key);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    private string L(string key, params object[] args)
    {
        var value = _localizationService.GetString(key, args);
        return string.IsNullOrWhiteSpace(value) ? key : value;
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
