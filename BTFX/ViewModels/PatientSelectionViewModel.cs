using System.Collections.ObjectModel;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using ToolHelper.LoggingDiagnostics.Abstractions;
using Constants = BTFX.Common.Constants;

namespace BTFX.ViewModels;

/// <summary>
/// Patient Selection View Model
/// </summary>
public partial class PatientSelectionViewModel : ObservableObject
{
    /// <summary>
    /// 服务
    /// </summary>
    private readonly IPatientService _patientService;
    private readonly INavigationService _navigationService;
    private readonly ISessionService _sessionService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;
    private const double PatientRowHeight = 60;
    private const double PatientRowTopMargin = 7;
    private const int MinimumPageSize = 1;

    [ObservableProperty]
    private ObservableCollection<Patient> _patients = new();

    [ObservableProperty]
    private ObservableCollection<PatientRowItem> _patientRows = new();

    [ObservableProperty]
    private ObservableCollection<PageItem> _pageNumbers = new();

    [ObservableProperty]
    private bool _canGoPrevious;

    [ObservableProperty]
    private bool _canGoNext;

    [ObservableProperty]
    private Patient? _selectedPatient;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalRecords = 0;

    [ObservableProperty]
    private string _currentUserInfo = string.Empty;

    [ObservableProperty]
    private bool _canAddPatient;

    [ObservableProperty]
    private bool _canImportExport;

    /// <summary>
    /// 已选中的患者数量
    /// </summary>
    public int SelectedCount => PatientRows.Count(r => r.IsChecked);

    /// <summary>
    /// 全选状态：0=未选，1=部分选，2=全选
    /// </summary>
    public int SelectAllState
    {
        get
        {
            var count = SelectedCount;
            if (count == 0) return 0;
            return count == PatientRows.Count ? 2 : 1;
        }
    }

    private List<Patient> _allPatients = new();
    private readonly HashSet<int> _selectedPatientIds = new();
    private int _pageSize = Constants.PATIENT_PAGE_SIZE;

    /// <summary>
    /// Constructor
    /// </summary>
    public PatientSelectionViewModel(
        IPatientService patientService,
        INavigationService navigationService,
        ISessionService sessionService,
        ILocalizationService localizationService)
    {
        _patientService = patientService;
        _navigationService = navigationService;
        _sessionService = sessionService;
        _localizationService = localizationService;

        // Try to get log service
        try
        {
            _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
        }
        catch { }

        // Set current user info
        var user = _sessionService.CurrentUser;
        if (user != null)
        {
            CurrentUserInfo = $"{user.Username} ({GetRoleDisplayName(user.Role)})";
        }

        // Set permissions
        CanAddPatient = user?.Role == UserRole.Administrator || user?.Role == UserRole.Operator;
        CanImportExport = user?.Role == UserRole.Administrator || user?.Role == UserRole.Operator;

        // Load patients
        _ = LoadPatientsAsync();
    }

    /// <summary>
    /// Load patients加载患者
    /// </summary>
    private async Task LoadPatientsAsync()
    {
        try
        {
            // Get all patients from service
            var allPatients = await _patientService.GetAllPatientsAsync();
            _allPatients = allPatients.Where(p => p.Status == PatientStatus.Active).ToList();

            // Apply search filter if needed
            ApplySearchFilter();

            _logHelper?.Information($"Loaded {_allPatients.Count} patients");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("Failed to load patients", ex);
        }
    }

    /// <summary>
    /// Apply search filter and pagination
    /// </summary>
    private void ApplySearchFilter()
    {
        IEnumerable<Patient> filtered = _allPatients;//开始时，过滤集合是所有患者

        // Apply search//如果搜索框输入不为空，则进行过滤
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.Trim().ToLower();//将搜索文本转换为小写以进行不区分大小写的比较
            filtered = filtered.Where(p =>
                p.Name.ToLower().Contains(searchLower) ||
                (p.Phone != null && p.Phone.Contains(searchLower)) ||
                (p.IdNumber != null && p.IdNumber.ToLower().Contains(searchLower)));//过滤患者列表，保留名称、电话或身份证号包含搜索文本的患者
        }

        var filteredList = filtered.ToList();//将过滤后的结果转换为列表以便后续处理
        TotalRecords = filteredList.Count;
        TotalPages = TotalRecords == 0 ? 0 : (int)Math.Ceiling(TotalRecords / (double)_pageSize);//计算总页数

        // Ensure current page is valid
        if (CurrentPage > TotalPages && TotalPages > 0)
        {
            CurrentPage = TotalPages;
        }
        if (CurrentPage < 1)
        {
            CurrentPage = 1;
        }

        // Get current page data
        var pageData = filteredList
            .Skip((CurrentPage - 1) * _pageSize)
            .Take(_pageSize)
            .ToList();//使用Skip和Take方法获取当前页的数据

        Patients.Clear();
        PatientRows.Clear();
        var startIndex = (CurrentPage - 1) * _pageSize + 1;
        for (int i = 0; i < pageData.Count; i++)
        {
            Patients.Add(pageData[i]);
            PatientRows.Add(new PatientRowItem
            {
                DisplayIndex = startIndex + i,
                Patient = pageData[i],
                IsChecked = _selectedPatientIds.Contains(pageData[i].Id)
            });
        }

        SelectedPatient = PatientRows.FirstOrDefault(r => r.IsChecked)?.Patient;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectAllState));

        // 更新分页导航状态
        CanGoPrevious = CurrentPage > 1;
        CanGoNext = CurrentPage < TotalPages;

        // 更新页码列表
        BuildPageNumbers();
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

        var rowFullHeight = PatientRowHeight + PatientRowTopMargin;
        var calculatedPageSize = Math.Max(MinimumPageSize, (int)Math.Floor((viewportHeight + PatientRowTopMargin) / rowFullHeight));

        if (calculatedPageSize == _pageSize)
        {
            return;
        }

        _pageSize = calculatedPageSize;

        if (TotalRecords > 0)
        {
            var maxPage = (int)Math.Ceiling(TotalRecords / (double)_pageSize);
            if (CurrentPage > maxPage)
            {
                CurrentPage = maxPage;
            }
        }

        ApplySearchFilter();
        _logHelper?.Information($"患者列表每页条数已根据可视高度更新为 {_pageSize}。");
    }

    /// <summary>
    /// 构建分页页码项集合（支持省略号）
    /// </summary>
    private void BuildPageNumbers()
    {
        PageNumbers.Clear();
        if (TotalPages == 0) return;

        // 始终显示：首页、尾页、当前页及其前后各1页，其余用省略号
        var pagesToShow = new SortedSet<int>();
        pagesToShow.Add(1);
        pagesToShow.Add(TotalPages);
        for (int p = Math.Max(1, CurrentPage - 1); p <= Math.Min(TotalPages, CurrentPage + 1); p++)
            pagesToShow.Add(p);

        int prev = 0;
        foreach (var page in pagesToShow)
        {
            if (prev > 0 && page - prev > 1)
            {
                PageNumbers.Add(new PageItem { DisplayText = "...", IsEllipsis = true, PageNumber = -1 });
            }
            PageNumbers.Add(new PageItem
            {
                DisplayText = page.ToString(),
                PageNumber = page,
                IsCurrent = page == CurrentPage
            });
            prev = page;
        }
    }

    /// <summary>
    /// Search command
    /// </summary>
    [RelayCommand]
    private void Search()
    {
        CurrentPage = 1;
        _selectedPatientIds.Clear();
        ApplySearchFilter();
        _logHelper?.Information($"Search patients: {SearchText}");
    }

    /// <summary>
    /// Add patient command
    /// </summary>
    [RelayCommand]
    private async Task AddPatientAsync()
    {
        try
        {
            var dialog = App.Services?.GetService(typeof(Views.Dialogs.PatientEditDialog)) as Views.Dialogs.PatientEditDialog;
            var viewModel = App.Services?.GetService(typeof(PatientEditViewModel)) as PatientEditViewModel;

            if (dialog != null && viewModel != null)
            {
                viewModel.InitializeForAdd();
                dialog.DataContext = viewModel;

                var result = await DialogHost.Show(dialog, "RootDialog");
                if (result is true)
                {
                    await LoadPatientsAsync();
                    _logHelper?.Information("Patient added successfully");
                }
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error("Failed to open add patient dialog", ex);
        }
    }

    /// <summary>
    /// Edit patient command
    /// </summary>
    [RelayCommand]
    private async Task EditPatientAsync(Patient? patient)
    {
        if (patient == null) return;

        try
        {
            var dialog = App.Services?.GetService(typeof(Views.Dialogs.PatientEditDialog)) as Views.Dialogs.PatientEditDialog;
            var viewModel = App.Services?.GetService(typeof(PatientEditViewModel)) as PatientEditViewModel;

            if (dialog != null && viewModel != null)
            {
                viewModel.InitializeForEdit(patient);
                dialog.DataContext = viewModel;

                var result = await DialogHost.Show(dialog, "RootDialog");
                if (result is true)
                {
                    await LoadPatientsAsync();
                    _logHelper?.Information($"Patient edited successfully: {patient.Name}");
                }
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"Failed to open edit patient dialog for: {patient.Name}", ex);
        }
    }

    /// <summary>
    /// Delete patient command
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeleteSelectedPatient))]
    private async Task DeletePatientAsync(Patient? patient)
    {
        if (patient == null) return;

        var result = await ShowConfirmDialogAsync(
            _localizationService.GetString("ConfirmDelete"),
            _localizationService.GetString("ConfirmDeletePatient"));

        if (!result)
            return;

        try
        {
            // Logical delete
            await _patientService.DeletePatientAsync(patient.Id);

            _logHelper?.Information($"Deleted patient: {patient.Name} (ID: {patient.Id})");

            // Reload
            await LoadPatientsAsync();

            // If current page is empty after delete, go to previous page
            if (Patients.Count == 0 && CurrentPage > 1)
            {
                CurrentPage--;
                ApplySearchFilter();
            }
        }
        catch (Exception ex)
        {
            _logHelper?.Error($"Failed to delete patient: {patient.Name}", ex);
            await ShowNoticeDialogAsync(
                _localizationService.GetString("DeleteFailedError"),
                _localizationService.GetString("Error"));
        }
    }

    /// <summary>
    /// 显示确认对话框。
    /// </summary>
    private static async Task<bool> ShowConfirmDialogAsync(string title, string message)
    {
        var result = await DialogHost.Show(
            new Views.Dialogs.ConfirmDialog
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
    private static Task ShowNoticeDialogAsync(string message, string title)
    {
        return DialogHost.Show(
            new Views.Dialogs.ConfirmDialog
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

    /// <summary>
    /// Can delete selected patient
    /// </summary>
    private bool CanDeleteSelectedPatient(Patient? patient)
    {
        if (patient == null) return false;

        var currentUser = _sessionService.CurrentUser;
        if (currentUser == null) return false;

        // Administrator can delete any patient
        if (currentUser.Role == UserRole.Administrator) return true;

        // Operator can only delete own patients
        if (currentUser.Role == UserRole.Operator)
            return patient.CreatedBy == currentUser.Id;

        return false;
    }

    /// <summary>
    /// Import command
    /// </summary>
    [RelayCommand]
    private async Task ImportAsync()
    {
        // TODO: Implement import functionality in Phase 4
        _logHelper?.Information("Import button clicked");
    }

    /// <summary>
    /// Export command
    /// </summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        // TODO: Implement export functionality in Phase 4
        _logHelper?.Information("Export button clicked");
    }

    /// <summary>
    /// Confirm select command
    /// </summary>
    [RelayCommand]
    private void ConfirmSelect()
    {
        if (SelectedPatient == null)
        {
            System.Windows.MessageBox.Show(
                "请先选择一个患者",
                "提示",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        // Set current patient
            _sessionService.SetCurrentPatient(SelectedPatient);

            _logHelper?.Information($"Selected patient: {SelectedPatient.Name} (ID: {SelectedPatient.Id})");

            // Navigate to main container
            _navigationService.NavigateTo("MainContainerViewModel");
        }

    /// <summary>
    /// Back command
    /// </summary>
    [RelayCommand]
    private void Back()
    {
        // Clear session
        _sessionService.ClearSession();

        _logHelper?.Information("Back to login");

        // Navigate to login using ViewModel type name
        _navigationService.NavigateTo<LoginViewModel>();
    }

    /// <summary>
    /// Previous page command
    /// </summary>
    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplySearchFilter();
        }
    }

    /// <summary>
    /// Next page command
    /// </summary>
    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            ApplySearchFilter();
        }
    }

    // 用于双击检测
    private PatientRowItem? _lastClickedRow;
    private DateTime _lastClickTime = DateTime.MinValue;
    private static readonly TimeSpan DoubleClickThreshold = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Toggle patient row selection command（单击选中；双击直接进入）
    /// </summary>
    [RelayCommand]
    private void ToggleSelect(PatientRowItem? row)
    {
        if (row == null) return;

        var now = DateTime.Now;
        bool isDoubleClick = row == _lastClickedRow && (now - _lastClickTime) < DoubleClickThreshold;
        _lastClickedRow = row;
        _lastClickTime = now;

        if (isDoubleClick)
        {
            // 双击：直接选中并进入
            row.IsChecked = true;
            _selectedPatientIds.Add(row.Patient.Id);
            SelectedPatient = row.Patient;
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectAllState));
            ConfirmSelect();
            return;
        }

        // 单击：切换选中状态
        var newState = !row.IsChecked;
        row.IsChecked = newState;
        if (newState)
        {
            _selectedPatientIds.Add(row.Patient.Id);
        }
        else
        {
            _selectedPatientIds.Remove(row.Patient.Id);
        }

        SelectedPatient = PatientRows.FirstOrDefault(r => r.IsChecked)?.Patient;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectAllState));
    }

    /// <summary>
    /// Patient double click command（保留以兼容 XAML 绑定，实际由 ToggleSelect 处理）
    /// </summary>
    [RelayCommand]
    private void PatientDoubleClick(Patient? patient)
    {
        // 双击逻辑已在 ToggleSelect 中通过时间检测处理，此处作为备用
        if (patient != null)
        {
            SelectedPatient = patient;
            ConfirmSelect();
        }
    }

    /// <summary>
    /// Toggle select all command
    /// </summary>
    [RelayCommand]
    private void ToggleSelectAll()
    {
        var allSelected = SelectAllState == 2;
        foreach (var r in PatientRows)
        {
            r.IsChecked = !allSelected;
            if (!allSelected)
            {
                _selectedPatientIds.Add(r.Patient.Id);
            }
            else
            {
                _selectedPatientIds.Remove(r.Patient.Id);
            }
        }

        SelectedPatient = allSelected ? null : PatientRows.FirstOrDefault()?.Patient;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(SelectAllState));
    }

    /// <summary>
    /// Clear search text command
    /// </summary>
    [RelayCommand]
    private void ClearSearchText()
    {
        SearchText = string.Empty;
        CurrentPage = 1;
        _selectedPatientIds.Clear();
        ApplySearchFilter();
    }

    /// <summary>
    /// Go to specific page command
    /// </summary>
    [RelayCommand]
    private void GoToPage(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > TotalPages || pageNumber == CurrentPage) return;
        CurrentPage = pageNumber;
        ApplySearchFilter();
    }

    /// <summary>
    /// Get role display name
    /// </summary>
    private string GetRoleDisplayName(UserRole role)
    {
        return role switch
        {
            UserRole.Administrator => "管理员",
            UserRole.Operator => "操作员",
            UserRole.Guest => "游客",
            _ => "未知"
        };
    }
}
