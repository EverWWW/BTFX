using System.Net.Http;
using System.Windows;
using System.Xml;
using BTFX.Common;
using BTFX.Services.Interfaces;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.Services.Implementations;

/// <summary>
/// 参考通用框架的在线更新检查服务。当前阶段只检查和提示，不执行覆盖更新。
/// </summary>
public class AppUpdateService : IAppUpdateService
{
    private readonly ISettingsService _settingsService;
    private readonly ILogHelper? _logHelper;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(20) };

    public AppUpdateService(ISettingsService settingsService, ILogHelper? logHelper = null)
    {
        _settingsService = settingsService;
        _logHelper = logHelper;
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.CurrentSettings.Update;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.UpdateUrl))
        {
            return;
        }

        if (DateTime.TryParse(settings.LastCheckDate, out var lastCheck)
            && lastCheck.Date.AddDays(Math.Max(1, settings.CheckIntervalDays)) > DateTime.Now.Date)
        {
            return;
        }

        try
        {
            var xmlText = await _httpClient.GetStringAsync(settings.UpdateUrl, cancellationToken);
            var xml = new XmlDocument();
            xml.LoadXml(xmlText);
            var node = xml.SelectSingleNode($"update/{Constants.APP_NAME}");
            if (node?.Attributes?["version"] == null)
            {
                settings.LastCheckDate = DateTime.Now.ToString("yyyy-MM-dd");
                _settingsService.SaveSettings();
                return;
            }

            var latestVersionText = node.Attributes["version"]!.Value;
            var latestVersion = new Version(latestVersionText);
            var currentVersion = new Version(Constants.VERSION_FULL.TrimStart('V', 'v'));
            if (latestVersion <= currentVersion)
            {
                settings.LastCheckDate = DateTime.Now.ToString("yyyy-MM-dd");
                _settingsService.SaveSettings();
                return;
            }

            var packageUrl = node.Attributes["url"]?.Value ?? string.Empty;
            var detail = node.InnerText?.Trim();
            var message = settings.ShowDetail && !string.IsNullOrWhiteSpace(detail)
                ? $"发现新版本 {latestVersionText}。\n\n更新内容：\n{detail}\n\n更新包：{packageUrl}"
                : $"发现新版本 {latestVersionText}。\n\n更新包：{packageUrl}";

            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "发现新版本", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
        catch (Exception ex)
        {
            _logHelper?.Warning($"检查在线更新失败：{ex.Message}");
        }
    }
}
