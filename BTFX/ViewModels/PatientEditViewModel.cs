using BTFX.Common;
using BTFX.Models;
using BTFX.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ToolHelper.LoggingDiagnostics.Abstractions;

namespace BTFX.ViewModels;

/// <summary>
/// Patient Edit Dialog ViewModel
/// </summary>
public partial class PatientEditViewModel : ObservableObject
{
    private readonly IPatientService _patientService;
    private readonly ISessionService _sessionService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogHelper? _logHelper;

    [ObservableProperty]
    private string _dialogTitle = string.Empty;

    [ObservableProperty]
    private int _patientId;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Gender _gender = Gender.Male;

    [ObservableProperty]
    private DateTime? _birthDate;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _idNumber = string.Empty;

    [ObservableProperty]
    private double? _height;

    [ObservableProperty]
    private double? _weight;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _medicalHistory = string.Empty;

    [ObservableProperty]
    private string _remark = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isSaving;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShouldClose))]
    private bool? _dialogResult;

    /// <summary>
    /// Indicates if the dialog should close
    /// </summary>
    public bool ShouldClose => DialogResult.HasValue;

    private bool _isEditMode;

    /// <summary>
    /// Constructor
    /// </summary>
    public PatientEditViewModel(
        IPatientService patientService,
        ISessionService sessionService,
        ILocalizationService localizationService)
    {
        _patientService = patientService;
        _sessionService = sessionService;
        _localizationService = localizationService;

            try
            {
                _logHelper = App.Services?.GetService(typeof(ILogHelper)) as ILogHelper;
            }
            catch { }
        }

        /// <summary>
        /// Called when Name property changes
        /// </summary>
        partial void OnNameChanged(string value)
        {
            ValidateInputRealtime();
        }

        /// <summary>
        /// Called when Phone property changes
        /// </summary>
        partial void OnPhoneChanged(string value)
        {
            ValidateInputRealtime();
        }

        /// <summary>
        /// Called when IdNumber property changes
        /// </summary>
        partial void OnIdNumberChanged(string value)
        {
            ValidateInputRealtime();
        }

        /// <summary>
        /// Called when Height property changes
        /// </summary>
        partial void OnHeightChanged(double? value)
        {
            ValidateInputRealtime();
        }

        /// <summary>
        /// Called when Weight property changes
        /// </summary>
        partial void OnWeightChanged(double? value)
        {
            ValidateInputRealtime();
        }

        /// <summary>
        /// Real-time validation (updates error message as user fixes issues)
        /// </summary>
        private void ValidateInputRealtime()
        {
            // If there's an error message, check if we should update it
            if (!string.IsNullOrWhiteSpace(ErrorMessage))
            {
                // Try to find the first validation error
                var newErrorMessage = GetFirstValidationError();

                // Update error message
                // - If no errors, clear it
                // - If different error, update to show new error
                ErrorMessage = newErrorMessage ?? string.Empty;
            }
        }

        /// <summary>
        /// Get the first validation error (returns null if all valid)
        /// </summary>
        private string? GetFirstValidationError()
        {
            // Check Name
            if (string.IsNullOrWhiteSpace(Name))
            {
                return _localizationService.GetString("PatientNameRequired");
            }

            if (Name.Length < 2 || Name.Length > 50)
            {
                return _localizationService.GetString("NameLengthError");
            }

            // Check Phone
            if (string.IsNullOrWhiteSpace(Phone))
            {
                return _localizationService.GetString("PhoneRequired");
            }

            if (Phone.Length < 8 || Phone.Length > 20)
            {
                return _localizationService.GetString("PhoneLengthError");
            }

            // Check IdNumber (optional, but if present must be valid)
            if (!string.IsNullOrWhiteSpace(IdNumber) && IdNumber.Length != 18 && IdNumber.Length != 15)
            {
                return _localizationService.GetString("IdNumberLengthError");
            }

            // Check Height (optional, but if present must be valid)
            if (Height.HasValue && (Height.Value < 50 || Height.Value > 250))
            {
                return _localizationService.GetString("HeightRangeError");
            }

            // Check Weight (optional, but if present must be valid)
            if (Weight.HasValue && (Weight.Value < 20 || Weight.Value > 300))
            {
                return _localizationService.GetString("WeightRangeError");
            }

            // All fields are valid
            return null;
        }

    /// <summary>
    /// Initialize for adding new patient
    /// </summary>
    public void InitializeForAdd()
    {
        _isEditMode = false;
        DialogTitle = _localizationService.GetString("AddPatient");
        PatientId = 0;
        ClearForm();
    }

    /// <summary>
    /// Initialize for editing existing patient
    /// </summary>
    public void InitializeForEdit(Patient patient)
    {
        _isEditMode = true;
        DialogTitle = _localizationService.GetString("EditPatient");
        PatientId = patient.Id;
        Name = patient.Name;
        Gender = patient.Gender;
        BirthDate = patient.BirthDate;
        Phone = patient.Phone;
        IdNumber = patient.IdNumber ?? string.Empty;
        Height = patient.Height;
        Weight = patient.Weight;
        Address = patient.Address ?? string.Empty;
        MedicalHistory = patient.MedicalHistory ?? string.Empty;
        Remark = patient.Remark ?? string.Empty;
        ErrorMessage = string.Empty; // Clear any previous error messages
    }

    /// <summary>
    /// Clear form
    /// </summary>
    private void ClearForm()
    {
        Name = string.Empty;
        Gender = Gender.Male;
        BirthDate = null;
        Phone = string.Empty;
        IdNumber = string.Empty;
        Height = null;
        Weight = null;
        Address = string.Empty;
        MedicalHistory = string.Empty;
        Remark = string.Empty;
        ErrorMessage = string.Empty;
    }

    /// <summary>
    /// Validate input
    /// </summary>
    private bool ValidateInput()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = _localizationService.GetString("PatientNameRequired");
            return false;
        }

        if (Name.Length < 2 || Name.Length > 50)
        {
            ErrorMessage = _localizationService.GetString("NameLengthError");
            return false;
        }

        if (string.IsNullOrWhiteSpace(Phone))
        {
            ErrorMessage = _localizationService.GetString("PhoneRequired");
            return false;
        }

        if (Phone.Length < 8 || Phone.Length > 20)
        {
            ErrorMessage = _localizationService.GetString("PhoneLengthError");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(IdNumber) && IdNumber.Length != 18 && IdNumber.Length != 15)
        {
            ErrorMessage = _localizationService.GetString("IdNumberLengthError");
            return false;
        }

        if (Height.HasValue && (Height.Value < 50 || Height.Value > 250))
        {
            ErrorMessage = _localizationService.GetString("HeightRangeError");
            return false;
        }

        if (Weight.HasValue && (Weight.Value < 20 || Weight.Value > 300))
        {
            ErrorMessage = _localizationService.GetString("WeightRangeError");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Save command
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!ValidateInput())
            return;

        try
        {
            IsSaving = true;
            ErrorMessage = string.Empty;

            var patient = new Patient
            {
                Id = PatientId,
                Name = Name.Trim(),
                Gender = Gender,
                BirthDate = BirthDate,
                Phone = Phone.Trim(),
                IdNumber = string.IsNullOrWhiteSpace(IdNumber) ? null : IdNumber.Trim(),
                Height = Height,
                Weight = Weight,
                Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim(),
                MedicalHistory = string.IsNullOrWhiteSpace(MedicalHistory) ? null : MedicalHistory.Trim(),
                Remark = string.IsNullOrWhiteSpace(Remark) ? null : Remark.Trim(),
                Status = PatientStatus.Active
            };

            if (_isEditMode)
            {
                await _patientService.UpdatePatientAsync(patient);
                _logHelper?.Information($"Updated patient: {patient.Name} (ID: {patient.Id})");
            }
            else
            {
                var currentUser = _sessionService.CurrentUser;
                if (currentUser != null)
                {
                    patient.CreatedBy = currentUser.Id;
                }

                await _patientService.AddPatientAsync(patient);
                _logHelper?.Information($"Added new patient: {patient.Name}");
            }

                DialogResult = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = _localizationService.GetString("SaveFailedError", ex.Message);
                _logHelper?.Error("Failed to save patient", ex);
            }
            finally
            {
                IsSaving = false;
            }
    }

    /// <summary>
    /// Cancel command
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
    }
}
