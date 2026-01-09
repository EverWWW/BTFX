using System.Collections.ObjectModel;
using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.ViewModels;

/// <summary>
/// Patient Selection View Model
/// </summary>
public partial class PatientSelectionViewModel : ObservableObject
{
    private readonly IPatientService _patientService;
    private readonly INavigationService _navigationService;
    private readonly ISessionService _sessionService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;

    [ObservableProperty]
    private ObservableCollection<Patient> _patients = new();

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

    private List<Patient> _allPatients = new();
    private const int PageSize = Constants.PATIENT_PAGE_SIZE;

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
    /// Load patients
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
        IEnumerable<Patient> filtered = _allPatients;

        // Apply search
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.Trim().ToLower();
            filtered = filtered.Where(p =>
                p.Name.ToLower().Contains(searchLower) ||
                (p.Phone != null && p.Phone.Contains(searchLower)) ||
                (p.IdNumber != null && p.IdNumber.ToLower().Contains(searchLower)));
        }

        var filteredList = filtered.ToList();
        TotalRecords = filteredList.Count;
        TotalPages = (int)Math.Ceiling(TotalRecords / (double)PageSize);

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
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        Patients.Clear();
        foreach (var patient in pageData)
        {
            Patients.Add(patient);
        }
    }

    /// <summary>
    /// Search command
    /// </summary>
    [RelayCommand]
    private void Search()
    {
        CurrentPage = 1;
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

                var result = dialog.ShowDialog();
                if (result == true)
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

                var result = dialog.ShowDialog();
                if (result == true)
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

        // Show confirmation dialog
        var result = System.Windows.MessageBox.Show(
            _localizationService.GetString("ConfirmDeletePatient"),
            _localizationService.GetString("ConfirmDelete"),
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
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
            System.Windows.MessageBox.Show(
                _localizationService.GetString("DeleteFailedError"),
                _localizationService.GetString("Error"),
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
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
        _navigationService.NavigateTo("MainContainerView");
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

        // Navigate to login
        _navigationService.NavigateTo("LoginView");
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

    /// <summary>
    /// Patient double click command
    /// </summary>
    [RelayCommand]
    private void PatientDoubleClick(Patient? patient)
    {
        if (patient != null)
        {
            SelectedPatient = patient;
            ConfirmSelect();
        }
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
