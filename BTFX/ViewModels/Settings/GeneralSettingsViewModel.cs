using System.Collections.ObjectModel;
using BTFX.Models;
using BTFX.Helpers;
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
    private readonly ILogHelper? _logHelper;
    private bool _isInitializing = true;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    public ObservableCollection<LanguageOption> LanguageOptions { get; } =
    [
        new() { Value = Common.AppLanguage.ChineseSimplified, Display = "中文" },
        new() { Value = Common.AppLanguage.English, Display = "English" }
    ];

    public GeneralSettingsViewModel(
        ISettingsService settingsService,
        ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;

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

    [RelayCommand]
    private void SaveGeneralSettings()
    {
        try
        {
            _settingsService.SaveSettings();
            AppDialog.Show(_localizationService.GetString("SaveSuccess"), _localizationService.GetString("Information"),
                AppDialogButtons.Ok, AppDialogIcon.Information);
            _logHelper?.Information("保存通用设置");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("保存通用设置失败", ex);
            AppDialog.Show(string.Format(_localizationService.GetString("SaveExceptionFormat"), ex.Message), _localizationService.GetString("Error"),
                AppDialogButtons.Ok, AppDialogIcon.Error);
        }
    }

    [RelayCommand]
    private async Task ExportSettingsAsync()
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = _localizationService.GetString("ExportSettings"),
                Filter = _localizationService.GetString("JsonFileFilter"),
                FileName = $"BTFX_Settings_{DateTime.Now:yyyyMMdd}",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true) return;

            IsSaving = true;
            var success = await _settingsService.ExportSettingsAsync(dialog.FileName);

            if (success)
            {
                AppDialog.Show(string.Format(_localizationService.GetString("SettingsExportSuccessFormat"), dialog.FileName), _localizationService.GetString("Information"),
                    AppDialogButtons.Ok, AppDialogIcon.Information);
                _logHelper?.Information($"设置导出成功：{dialog.FileName}");
            }
            else
            {
                AppDialog.Show(_localizationService.GetString("SettingsExportFailed"), _localizationService.GetString("Error"),
                    AppDialogButtons.Ok, AppDialogIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("设置导出失败", ex);
            AppDialog.Show(string.Format(_localizationService.GetString("ExportFailedFormat"), ex.Message), _localizationService.GetString("Error"),
                AppDialogButtons.Ok, AppDialogIcon.Error);
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
                Title = _localizationService.GetString("ImportSettings"),
                Filter = _localizationService.GetString("JsonFileFilter"),
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            var result = AppDialog.Show(
                _localizationService.GetString("ConfirmImportSettings"),
                _localizationService.GetString("ConfirmImport"),
                AppDialogButtons.YesNo,
                AppDialogIcon.Question);

            if (result != AppDialogResult.Yes) return;

            IsSaving = true;
            var success = await _settingsService.ImportSettingsAsync(dialog.FileName);

            if (success)
            {
                _isInitializing = true;
                LoadSettings();
                _isInitializing = false;

                AppDialog.Show(_localizationService.GetString("SettingsImportSuccessRestartHint"), _localizationService.GetString("Information"),
                    AppDialogButtons.Ok, AppDialogIcon.Information);
                _logHelper?.Information($"设置导入成功：{dialog.FileName}");
            }
            else
            {
                AppDialog.Show(_localizationService.GetString("SettingsImportFailedCheckFormat"), _localizationService.GetString("Error"),
                    AppDialogButtons.Ok, AppDialogIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("设置导入失败", ex);
            AppDialog.Show(string.Format(_localizationService.GetString("ImportFailedFormat"), ex.Message), _localizationService.GetString("Error"),
                AppDialogButtons.Ok, AppDialogIcon.Error);
        }
        finally
        {
            IsSaving = false;
        }
    }
}
