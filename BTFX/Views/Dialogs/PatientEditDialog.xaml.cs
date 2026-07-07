using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;
using MaterialDesignThemes.Wpf;

namespace BTFX.Views.Dialogs;

/// <summary>
/// PatientEditDialog.xaml Interaction Logic
/// </summary>
public partial class PatientEditDialog : UserControl
{
    private PatientEditViewModel? _currentViewModel;
    private ILocalizationService? _localizationService;

    public PatientEditDialog()
    {
        InitializeComponent();
        Loaded += PatientEditDialog_Loaded;
        Unloaded += PatientEditDialog_Unloaded;
        DataContextChanged += PatientEditDialog_DataContextChanged;
    }

    private void PatientEditDialog_Loaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel(DataContext as PatientEditViewModel);
        SetCalendarLanguage();

        if (_localizationService == null
            && App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
        {
            _localizationService = localizationService;
            _localizationService.LanguageChanged += OnLanguageChanged;
        }

        SyncBirthDateDisplay();
    }

    private void PatientEditDialog_Unloaded(object sender, RoutedEventArgs e)
    {
        AttachViewModel(null);

        if (_localizationService != null)
        {
            _localizationService.LanguageChanged -= OnLanguageChanged;
            _localizationService = null;
        }
    }

    private void PatientEditDialog_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AttachViewModel(e.NewValue as PatientEditViewModel);
        SyncBirthDateDisplay();
    }

    private void AttachViewModel(PatientEditViewModel? viewModel)
    {
        if (ReferenceEquals(_currentViewModel, viewModel))
        {
            return;
        }

        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _currentViewModel = viewModel;

        if (_currentViewModel != null)
        {
            _currentViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PatientEditViewModel.ShouldClose))
        {
            if (DataContext is PatientEditViewModel viewModel && viewModel.ShouldClose)
            {
                DialogHost.Close("RootDialog", viewModel.DialogResult);
            }
        }
        else if (e.PropertyName == nameof(PatientEditViewModel.BirthDate))
        {
            SyncBirthDateDisplay();
        }
    }

    private void OnLanguageChanged(object? sender, Common.AppLanguage language)
    {
        SetCalendarLanguage();
        SyncBirthDateDisplay();
    }

    private void SetCalendarLanguage()
    {
        var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        BirthDateCalendar.Language = XmlLanguage.GetLanguage(culture.Name);
        BirthDateCalendar.DisplayDateEnd = DateTime.Today;
        BirthDateCalendar.BlackoutDates.Clear();
        BirthDateCalendar.BlackoutDates.Add(new CalendarDateRange(DateTime.Today.AddDays(1), DateTime.Today.AddYears(120)));
    }

    private void BirthDateButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is PatientEditViewModel vm && vm.BirthDate.HasValue)
        {
            BirthDateCalendar.SelectedDate = vm.BirthDate.Value;
            BirthDateCalendar.DisplayDate = vm.BirthDate.Value;
        }
        else
        {
            BirthDateCalendar.SelectedDate = null;
            BirthDateCalendar.DisplayDate = DateTime.Today;
        }

        BirthDatePopup.IsOpen = true;
    }

    private void BirthDateCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PatientEditViewModel vm && BirthDateCalendar.SelectedDate.HasValue)
        {
            var selectedDate = BirthDateCalendar.SelectedDate.Value.Date;
            if (selectedDate > DateTime.Today)
            {
                selectedDate = DateTime.Today;
            }

            vm.BirthDate = selectedDate;
            BirthDatePopup.IsOpen = false;
            SyncBirthDateDisplay();
        }
    }

    private void SyncBirthDateDisplay()
    {
        if (BirthDateText == null || BirthDateCalendar == null)
        {
            return;
        }

        if (DataContext is PatientEditViewModel vm && vm.BirthDate.HasValue)
        {
            var birthDate = vm.BirthDate.Value.Date > DateTime.Today ? DateTime.Today : vm.BirthDate.Value.Date;
            BirthDateText.Text = birthDate.ToString("yyyy/M/d");
            BirthDateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#333333"));
            BirthDateCalendar.SelectedDate = birthDate;
            BirthDateCalendar.DisplayDate = birthDate;
        }
        else
        {
            BirthDateText.Text = _localizationService?.GetString("PleaseSelectDate")
                ?? Application.Current.TryFindResource("PleaseSelectDate") as string
                ?? "请选择日期";
            BirthDateText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999"));
            BirthDateCalendar.SelectedDate = null;
        }
    }
}

