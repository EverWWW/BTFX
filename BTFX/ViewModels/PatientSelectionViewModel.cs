using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
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
    private readonly IExportImportService _exportImportService;
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
    [NotifyPropertyChangedFor(nameof(CanExportPatients))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
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
        ILocalizationService localizationService,
        IExportImportService exportImportService)
    {
        _patientService = patientService;
        _navigationService = navigationService;
        _sessionService = sessionService;
        _localizationService = localizationService;
        _exportImportService = exportImportService;

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
        OnSelectionStateChanged();

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
            _localizationService.GetString("ConfirmDeletePatient"),
            "TrashCanOutline");

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
    private static async Task<bool> ShowConfirmDialogAsync(string title, string message, string iconKind = "HelpCircleOutline")
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
                    IsCancelVisible = true,
                    IconKind = iconKind
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
                    IsCancelVisible = false,
                    IconKind = "InformationOutline"
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

        return currentUser.Role == UserRole.Operator;
    }

    /// <summary>
    /// Import command
    /// </summary>
    [RelayCommand]
    private async Task ImportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "导入患者基础资料",
            Filter = "患者资料文件 (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|Excel 文件 (*.xlsx;*.xls)|*.xlsx;*.xls|CSV 文件 (*.csv)|*.csv",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var result = await RunWithProgressDialogAsync(
                "导入患者基础资料",
                "正在导入",
                "正在读取患者资料文件...",
                (progress, token) => ImportPatientsFromFileAsync(dialog.FileName, progress, token));

            await LoadPatientsAsync();
            await ShowNoticeDialogAsync(
                $"导入完成：新增 {result.AddedCount} 条，更新 {result.UpdatedCount} 条，跳过 {result.SkippedCount} 条。",
                "提示");
            _logHelper?.Information($"患者资料导入完成：新增={result.AddedCount}, 更新={result.UpdatedCount}, 跳过={result.SkippedCount}");
        }
        catch (OperationCanceledException)
        {
            await ShowNoticeDialogAsync("导入已取消。", "提示");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("患者资料导入失败", ex);
            await ShowNoticeDialogAsync($"导入失败：{ex.Message}", "错误");
        }
    }

    public bool CanExportPatients => CanImportExport && SelectedCount > 0;

    /// <summary>
    /// Export command
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportPatients))]
    private async Task ExportAsync()
    {
        var exportPatients = GetPatientsForExport();
        if (exportPatients.Count == 0)
        {
            await ShowNoticeDialogAsync("请先选择要导出的患者。", "提示");
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出患者基础资料",
            Filter = "Excel 文件 (*.xlsx)|*.xlsx|CSV 文件 (*.csv)|*.csv",
            FileName = $"患者基础资料_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var format = dialog.FilterIndex == 2 ? ExportFormat.CSV : ExportFormat.Excel;
            var filePath = EnsureExportExtension(dialog.FileName, format);
            var success = await RunWithProgressDialogAsync(
                "导出患者基础资料",
                "正在导出",
                $"正在导出 {exportPatients.Count} 条患者资料...",
                async (progress, token) =>
                {
                    progress.Report(new OperationProgressInfo(20, "准备数据", "正在整理患者基础资料..."));
                    token.ThrowIfCancellationRequested();
                    await Task.Delay(120, token);
                    progress.Report(new OperationProgressInfo(55, "写入文件", $"正在写入 {Path.GetFileName(filePath)}..."));
                    var exported = await _exportImportService.ExportPatientsAsync(exportPatients, format, filePath);
                    progress.Report(new OperationProgressInfo(92, "完成校验", "正在确认导出文件..."));
                    token.ThrowIfCancellationRequested();
                    return exported;
                });

            await ShowNoticeDialogAsync(success ? $"导出成功：{filePath}" : "导出失败，请检查文件是否被占用。", success ? "提示" : "错误");
            _logHelper?.Information($"患者资料导出：Count={exportPatients.Count}, File={filePath}, Success={success}");
        }
        catch (OperationCanceledException)
        {
            await ShowNoticeDialogAsync("导出已取消。", "提示");
        }
        catch (Exception ex)
        {
            _logHelper?.Error("患者资料导出失败", ex);
            await ShowNoticeDialogAsync($"导出失败：{ex.Message}", "错误");
        }
    }

    private List<Patient> GetPatientsForExport()
    {
        return _allPatients
            .Where(patient => _selectedPatientIds.Contains(patient.Id))
            .OrderByDescending(patient => patient.CreatedAt)
            .ToList();
    }

    private static string EnsureExportExtension(string filePath, ExportFormat format)
    {
        var expectedExtension = format == ExportFormat.CSV ? ".csv" : ".xlsx";
        return Path.GetExtension(filePath).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase)
            ? filePath
            : Path.ChangeExtension(filePath, expectedExtension);
    }

    private IEnumerable<Patient> GetFilteredPatients()
    {
        IEnumerable<Patient> filtered = _allPatients;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.Trim().ToLower();
            filtered = filtered.Where(p =>
                p.Name.ToLower().Contains(searchLower) ||
                (!string.IsNullOrWhiteSpace(p.Phone) && p.Phone.Contains(searchLower)) ||
                (!string.IsNullOrWhiteSpace(p.IdNumber) && p.IdNumber.ToLower().Contains(searchLower)));
        }

        return filtered.OrderByDescending(patient => patient.CreatedAt);
    }

    private async Task<PatientImportResult> ImportPatientsFromFileAsync(
        string filePath,
        IProgress<OperationProgressInfo> progress,
        CancellationToken cancellationToken)
    {
        progress.Report(new OperationProgressInfo(10, "读取文件", "正在解析患者资料文件..."));
        var importedPatients = await _exportImportService.ImportPatientsAsync(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        progress.Report(new OperationProgressInfo(35, "校验数据", $"已读取 {importedPatients.Count} 条患者资料，正在校验..."));
        var existingPatients = await _patientService.GetAllPatientsAsync();
        var added = 0;
        var updated = 0;
        var skipped = 0;

        for (var index = 0; index < importedPatients.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var imported = importedPatients[index];
            var percent = 35 + 55.0 * (index + 1) / Math.Max(importedPatients.Count, 1);
            progress.Report(new OperationProgressInfo(percent, "写入数据", $"正在处理 {index + 1}/{importedPatients.Count}：{imported.Name}"));

            if (string.IsNullOrWhiteSpace(imported.Name) || imported.Height is not > 0)
            {
                skipped++;
                continue;
            }

            var existing = FindExistingPatient(existingPatients, imported);
            if (existing is null)
            {
                imported.CreatedBy = _sessionService.CurrentUser?.Id ?? 0;
                var id = await _patientService.AddPatientAsync(imported);
                if (id > 0)
                {
                    imported.Id = id;
                    existingPatients.Add(imported);
                    added++;
                }
                else
                {
                    skipped++;
                }
            }
            else
            {
                MergePatientForImport(existing, imported);
                if (await _patientService.UpdatePatientAsync(existing))
                {
                    updated++;
                }
                else
                {
                    skipped++;
                }
            }
        }

        progress.Report(new OperationProgressInfo(95, "刷新列表", "正在刷新患者列表..."));
        return new PatientImportResult(added, updated, skipped);
    }

    private static Patient? FindExistingPatient(IEnumerable<Patient> existingPatients, Patient imported)
    {
        if (!string.IsNullOrWhiteSpace(imported.IdNumber))
        {
            var byIdNumber = existingPatients.FirstOrDefault(patient =>
                !string.IsNullOrWhiteSpace(patient.IdNumber) &&
                string.Equals(patient.IdNumber.Trim(), imported.IdNumber.Trim(), StringComparison.OrdinalIgnoreCase));
            if (byIdNumber is not null)
            {
                return byIdNumber;
            }
        }

        if (!string.IsNullOrWhiteSpace(imported.Phone))
        {
            return existingPatients.FirstOrDefault(patient =>
                string.Equals(patient.Name.Trim(), imported.Name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(patient.Phone.Trim(), imported.Phone.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static void MergePatientForImport(Patient target, Patient source)
    {
        target.Name = source.Name;
        target.Gender = source.Gender;
        target.BirthDate = source.BirthDate ?? target.BirthDate;
        target.Phone = string.IsNullOrWhiteSpace(source.Phone) ? target.Phone : source.Phone;
        target.IdNumber = string.IsNullOrWhiteSpace(source.IdNumber) ? target.IdNumber : source.IdNumber;
        target.HospitalNumber = string.IsNullOrWhiteSpace(source.HospitalNumber) ? target.HospitalNumber : source.HospitalNumber;
        target.Height = source.Height ?? target.Height;
        target.Weight = source.Weight ?? target.Weight;
        target.Address = string.IsNullOrWhiteSpace(source.Address) ? target.Address : source.Address;
        target.MedicalHistory = string.IsNullOrWhiteSpace(source.MedicalHistory) ? target.MedicalHistory : source.MedicalHistory;
        target.Remark = string.IsNullOrWhiteSpace(source.Remark) ? target.Remark : source.Remark;
        target.Status = PatientStatus.Active;
    }

    private static async Task<T> RunWithProgressDialogAsync<T>(
        string title,
        string stage,
        string message,
        Func<IProgress<OperationProgressInfo>, CancellationToken, Task<T>> operation)
    {
        using var operationCts = new CancellationTokenSource();
        var progressViewModel = new OperationProgressDialogViewModel(
            title,
            stage,
            message,
            operationCts,
            canCancel: true);

        var progress = new Progress<OperationProgressInfo>(progressViewModel.Update);
        var dialog = new Views.Dialogs.OperationProgressDialog
        {
            DataContext = progressViewModel
        };

        var dialogTask = DialogHost.Show(dialog, "RootDialog");
        try
        {
            var result = await Task.Run(() => operation(progress, operationCts.Token), operationCts.Token);
            progressViewModel.MarkCompleted("操作已完成。");
            await Task.Delay(650);
            DialogHost.Close("RootDialog");
            await dialogTask;
            RestoreMainWindowIfMinimized();
            return result;
        }
        catch (OperationCanceledException)
        {
            progressViewModel.MarkFailed("操作已取消。");
            await Task.Delay(350);
            DialogHost.Close("RootDialog");
            await dialogTask;
            RestoreMainWindowIfMinimized();
            throw;
        }
        catch
        {
            progressViewModel.MarkFailed("操作执行失败。");
            await Task.Delay(350);
            DialogHost.Close("RootDialog");
            await dialogTask;
            RestoreMainWindowIfMinimized();
            throw;
        }
    }

    private static void RestoreMainWindowIfMinimized()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.Invoke(() =>
        {
            var window = Application.Current?.MainWindow;
            if (window is null)
            {
                return;
            }

            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
        });
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
            OnSelectionStateChanged();
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
        OnSelectionStateChanged();
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
        OnSelectionStateChanged();
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

    private void OnSelectionStateChanged()
    {
        OnPropertyChanged(nameof(CanExportPatients));
        ExportCommand.NotifyCanExecuteChanged();
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

public sealed record PatientImportResult(int AddedCount, int UpdatedCount, int SkippedCount);
