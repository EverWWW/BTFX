using System.Collections.ObjectModel;
using BTFX.Models;
using BTFX.Services.Interfaces;
using BTFX.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.ViewModels.Settings;

/// <summary>
/// 科室管理视图模型
/// </summary>
public partial class DepartmentManagementViewModel : ObservableObject
{
    private readonly IDepartmentService _departmentService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;

    [ObservableProperty]
    private ObservableCollection<DepartmentItem> _departments = [];

    [ObservableProperty]
    private DepartmentItem? _selectedDepartment;

    [ObservableProperty]
    private bool _isLoading;

    public DepartmentManagementViewModel(
        IDepartmentService departmentService,
        ILocalizationService localizationService)
    {
        _departmentService = departmentService;
        _localizationService = localizationService;

        try { _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper; } catch { }

        _ = LoadDepartmentsAsync();
    }

    [RelayCommand]
    private async Task LoadDepartmentsAsync()
    {
        try
        {
            IsLoading = true;
            var departments = await _departmentService.GetAllDepartmentsAsync();

            Departments.Clear();
            int rowNumber = 1;
            foreach (var dept in departments)
            {
                Departments.Add(new DepartmentItem(dept, rowNumber++));
            }

            _logHelper?.Information($"加载科室列表：共{departments.Count}个");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("加载科室列表失败", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddDepartmentAsync()
    {
        try
        {
            var vm = new DepartmentEditViewModel(_departmentService);
            var dialog = new DepartmentEditDialog(vm);
            var result = await DialogHost.Show(dialog, "RootDialog");

            if (result is true)
            {
                await LoadDepartmentsAsync();
                _logHelper?.Information("添加科室成功");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("添加科室失败", ex);
        }
    }

    [RelayCommand]
    private async Task EditDepartmentAsync(DepartmentItem? item)
    {
        if (item == null) return;

        try
        {
            var vm = new DepartmentEditViewModel(_departmentService, item.Department);
            var dialog = new DepartmentEditDialog(vm);
            var result = await DialogHost.Show(dialog, "RootDialog");

            if (result is true)
            {
                await LoadDepartmentsAsync();
                _logHelper?.Information($"编辑科室成功: {item.Department.Name}");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"编辑科室失败: {item.Department.Name}", ex);
        }
    }

    [RelayCommand]
    private async Task DeleteDepartmentAsync(DepartmentItem? item)
    {
        if (item == null) return;

        var result = System.Windows.MessageBox.Show(
            $"确定要删除科室 {item.Department.Name} 吗？此操作不可恢复！",
            "确认删除",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var success = await _departmentService.DeleteDepartmentAsync(item.Department.Id);
            if (success)
            {
                await LoadDepartmentsAsync();
                _logHelper?.Information($"删除科室成功: {item.Department.Name}");
            }
            else
            {
                System.Windows.MessageBox.Show("该科室正在被使用，无法删除", "警告",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除科室失败: {item.Department.Name}", ex);
            System.Windows.MessageBox.Show($"删除失败：{ex.Message}", "错误",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}
