using System.Collections.ObjectModel;
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
