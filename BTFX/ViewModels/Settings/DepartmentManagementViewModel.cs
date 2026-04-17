using System.Collections.ObjectModel;
using System.Linq;
using BTFX.Models;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;
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
    private const double DepartmentRowHeight = 60;
    private const double DepartmentRowTopMargin = 8;
    private const int MinimumPageSize = 2;
    private const int MaximumPageSize = 6;

    private readonly IDepartmentService _departmentService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;
    private readonly List<DepartmentItem> _allDepartments = [];
    private bool _isUpdatingSelection;

    private ObservableCollection<PageItem> _pageNumbers = [];
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _pageSize = MaximumPageSize;

    [ObservableProperty]
    private ObservableCollection<DepartmentItem> _departments = [];

    [ObservableProperty]
    private DepartmentItem? _selectedDepartment;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<PageItem> PageNumbers => _pageNumbers;

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            if (SetProperty(ref _totalPages, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }
    }

    public bool CanGoPrevious => _currentPage > 1;

    public bool CanGoNext => _currentPage < _totalPages;

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

            _allDepartments.Clear();
            var rowNumber = 1;
            foreach (var dept in departments)
            {
                _allDepartments.Add(new DepartmentItem(dept, rowNumber++));
            }

            CurrentPage = 1;
            RefreshPagedDepartments();

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

        var result = await ShowConfirmDialogAsync(
            "确认删除",
            $"确定要删除科室 {item.Department.Name} 吗？此操作不可恢复！");

        if (!result) return;

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
                await ShowNoticeDialogAsync("警告", "该科室正在被使用，无法删除");
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"删除科室失败: {item.Department.Name}", ex);
            await ShowNoticeDialogAsync("错误", $"删除失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 显示确认对话框。
    /// </summary>
    private static async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var result = await DialogHost.Show(
            new ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = title,
                    Message = message,
                    ConfirmText = "确定",
                    CancelText = "取消",
                    IsCancelVisible = true
                }
            },
            "RootDialog").ConfigureAwait(true);

        return result is true;
    }

    /// <summary>
    /// 显示提示对话框。
    /// </summary>
    private static Task ShowNoticeDialogAsync(string title, string message)
    {
        return DialogHost.Show(
            new ConfirmDialog
            {
                DataContext = new ConfirmDialogViewModel
                {
                    Title = title,
                    Message = message,
                    ConfirmText = "确定",
                    IsCancelVisible = false
                }
            },
            "RootDialog");
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (!CanGoPrevious)
        {
            return;
        }

        CurrentPage--;
        RefreshPagedDepartments();
    }

    [RelayCommand]
    private void NextPage()
    {
        if (!CanGoNext)
        {
            return;
        }

        CurrentPage++;
        RefreshPagedDepartments();
    }

    [RelayCommand]
    private void GoToPage(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > TotalPages || pageNumber == CurrentPage)
        {
            return;
        }

        CurrentPage = pageNumber;
        RefreshPagedDepartments();
    }

    /// <summary>
    /// 根据列表可视区域高度动态更新每页条数。
    /// </summary>
    /// <param name="viewportHeight">列表可视区域高度。</param>
    public void UpdatePageSize(double viewportHeight)
    {
        if (viewportHeight <= 0)
        {
            return;
        }

        var rowFullHeight = DepartmentRowHeight + DepartmentRowTopMargin;
        var calculatedPageSize = Math.Clamp(
            (int)Math.Floor((viewportHeight + DepartmentRowTopMargin) / rowFullHeight),
            MinimumPageSize,
            MaximumPageSize);

        if (calculatedPageSize == _pageSize)
        {
            return;
        }

        _pageSize = calculatedPageSize;

        if (_allDepartments.Count > 0)
        {
            var maxPage = (int)Math.Ceiling(_allDepartments.Count / (double)_pageSize);
            if (CurrentPage > maxPage)
            {
                CurrentPage = maxPage;
            }
        }

        RefreshPagedDepartments();
        _logHelper?.Information($"科室列表每页条数已根据可视高度更新为 {_pageSize}。");
    }

    private void RefreshPagedDepartments()
    {
        foreach (var item in _allDepartments)
        {
            item.IsChecked = false;
        }

        Departments.Clear();

        TotalPages = _allDepartments.Count == 0
            ? 1
            : (int)Math.Ceiling(_allDepartments.Count / (double)_pageSize);

        if (CurrentPage > TotalPages)
        {
            CurrentPage = TotalPages;
        }

        if (CurrentPage < 1)
        {
            CurrentPage = 1;
        }

        foreach (var item in _allDepartments.Skip((CurrentPage - 1) * _pageSize).Take(_pageSize))
        {
            item.IsChecked = SelectedDepartment != null && ReferenceEquals(item, SelectedDepartment);
            Departments.Add(item);
        }

        if (SelectedDepartment != null && !Departments.Contains(SelectedDepartment))
        {
            SelectedDepartment = null;
        }

        BuildPageNumbers();
    }

    private void BuildPageNumbers()
    {
        PageNumbers.Clear();
        if (TotalPages <= 0)
        {
            return;
        }

        var pagesToShow = new SortedSet<int> { 1, TotalPages };
        for (var page = Math.Max(1, CurrentPage - 1); page <= Math.Min(TotalPages, CurrentPage + 1); page++)
        {
            pagesToShow.Add(page);
        }

        var previousPage = 0;
        foreach (var page in pagesToShow)
        {
            if (previousPage > 0 && page - previousPage > 1)
            {
                PageNumbers.Add(new PageItem
                {
                    DisplayText = "...",
                    IsEllipsis = true,
                    PageNumber = -1
                });
            }

            PageNumbers.Add(new PageItem
            {
                DisplayText = page.ToString(),
                PageNumber = page,
                IsCurrent = page == CurrentPage
            });

            previousPage = page;
        }
    }

    partial void OnSelectedDepartmentChanged(DepartmentItem? value)
    {
        if (_isUpdatingSelection)
        {
            return;
        }

        try
        {
            _isUpdatingSelection = true;
            foreach (var item in _allDepartments)
            {
                item.IsChecked = value != null && ReferenceEquals(item, value);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }
}
