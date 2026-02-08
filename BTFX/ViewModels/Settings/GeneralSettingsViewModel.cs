using System.Collections.ObjectModel;
using System.Windows.Media;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolHelper.LoggingDiagnostics.Abstractions;
using BtfxConstants = BTFX.Common.Constants;

namespace BTFX.ViewModels.Settings;

/// <summary>
/// 通用设置视图模型
/// </summary>
public partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly ILogHelper? _logHelper;
    private bool _isInitializing = true;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private ThemeOption? _selectedTheme;

    public ObservableCollection<LanguageOption> LanguageOptions { get; } =
    [
        new() { Value = Common.AppLanguage.ChineseSimplified, Display = "简体中文" },
        new() { Value = Common.AppLanguage.English, Display = "English" }
    ];

    public ObservableCollection<ThemeOption> ThemeOptions { get; } =
    [
        new() { Value = Common.AppTheme.Light, Display = "浅色主题", IconKind = "WhiteBalanceSunny" },
        new() { Value = Common.AppTheme.Dark, Display = "深色主题", IconKind = "WeatherNight" }
    ];

    /// <summary>
    /// 可选主题色列表
    /// </summary>
    public ObservableCollection<ThemeColorOption> ThemeColorOptions { get; } =
    [
        new() { ColorHex = "#FF009EDB", DisplayName = "天蓝" },
        new() { ColorHex = "#FF2196F3", DisplayName = "蓝色" },
        new() { ColorHex = "#FF3F51B5", DisplayName = "靛蓝" },
        new() { ColorHex = "#FF673AB7", DisplayName = "紫色" },
        new() { ColorHex = "#FF009688", DisplayName = "青色" },
        new() { ColorHex = "#FF4CAF50", DisplayName = "绿色" },
        new() { ColorHex = "#FFFF9800", DisplayName = "橙色" },
        new() { ColorHex = "#FFE91E63", DisplayName = "粉红" },
        new() { ColorHex = "#FFF44336", DisplayName = "红色" },
        new() { ColorHex = "#FF607D8B", DisplayName = "蓝灰" }
    ];

    public GeneralSettingsViewModel(
        ISettingsService settingsService,
        ILocalizationService localizationService,
        IThemeService themeService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        _themeService = themeService;

        try { _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper; } catch { }

        LoadSettings();
        _isInitializing = false;
    }

    private void LoadSettings()
    {
        try
        {
            var settings = _settingsService.CurrentSettings;
            SelectedLanguage = LanguageOptions.FirstOrDefault(x => x.Value == settings.Application.Language);
            SelectedTheme = ThemeOptions.FirstOrDefault(x => x.Value == settings.Application.Theme);

            // 加载主题色选中状态
            var savedColor = settings.Application.PrimaryColor;
            foreach (var option in ThemeColorOptions)
            {
                option.IsSelected = string.Equals(option.ColorHex, savedColor, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载通用设置失败", ex);
        }
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (_isInitializing || value == null) return;
        _localizationService.ApplyLanguage(value.Value);
        _settingsService.CurrentSettings.Application.Language = value.Value;
        _settingsService.SaveSettings();
        _logHelper?.Information($"切换语言: {value.Display}");
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (_isInitializing || value == null) return;
        _themeService.ApplyTheme(value.Value);
        _settingsService.CurrentSettings.Application.Theme = value.Value;
        _settingsService.SaveSettings();
        _logHelper?.Information($"切换主题: {value.Display}");
    }

    /// <summary>
    /// 应用主题色
    /// </summary>
    [RelayCommand]
    private void ApplyThemeColor(ThemeColorOption? colorOption)
    {
        if (_isInitializing || colorOption == null) return;

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorOption.ColorHex);
            _themeService.SetPrimaryColor(color);

            // 更新选中状态
            foreach (var option in ThemeColorOptions)
            {
                option.IsSelected = option == colorOption;
            }

            // 保存配置
            _settingsService.CurrentSettings.Application.PrimaryColor = colorOption.ColorHex;
            _settingsService.SaveSettings();

            _logHelper?.Information($"切换主题色: {colorOption.DisplayName} ({colorOption.ColorHex})");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("切换主题色失败", ex);
        }
    }

    [RelayCommand]
    private void SaveGeneralSettings()
    {
        try
        {
            _settingsService.SaveSettings();
            System.Windows.MessageBox.Show("设置已保存！", "提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            _logHelper?.Information("保存通用设置");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("保存通用设置失败", ex);
            System.Windows.MessageBox.Show($"保存失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task ExportSettingsAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "导出设置",
                Filter = "JSON文件 (*.json)|*.json",
                FileName = $"BTFX_Settings_{DateTime.Now:yyyyMMdd}",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true) return;

            IsSaving = true;
            var success = await _settingsService.ExportSettingsAsync(dialog.FileName);

            if (success)
            {
                System.Windows.MessageBox.Show($"设置导出成功！\n文件：{dialog.FileName}", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"设置导出成功：{dialog.FileName}");
            }
            else
            {
                System.Windows.MessageBox.Show("设置导出失败", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("设置导出失败", ex);
            System.Windows.MessageBox.Show($"导出失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task ImportSettingsAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "导入设置",
                Filter = "JSON文件 (*.json)|*.json",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            var result = System.Windows.MessageBox.Show(
                "导入设置将覆盖当前的通用设置和单位设置，是否继续？",
                "确认导入",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            IsSaving = true;
            var success = await _settingsService.ImportSettingsAsync(dialog.FileName);

            if (success)
            {
                _isInitializing = true;
                LoadSettings();
                _isInitializing = false;

                System.Windows.MessageBox.Show("设置导入成功！\n部分设置可能需要重启应用后生效。", "提示",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                _logHelper?.Information($"设置导入成功：{dialog.FileName}");
            }
            else
            {
                System.Windows.MessageBox.Show("设置导入失败，请检查文件格式", "错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("设置导入失败", ex);
            System.Windows.MessageBox.Show($"导入失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }
}
