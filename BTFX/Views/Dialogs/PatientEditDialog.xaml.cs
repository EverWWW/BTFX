using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
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
        SetDatePickerLanguage();

        if (_localizationService == null
            && App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
        {
            _localizationService = localizationService;
            _localizationService.LanguageChanged += OnLanguageChanged;
        }
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
    }

    private void OnLanguageChanged(object? sender, Common.AppLanguage language)
    {
        SetDatePickerLanguage();
    }

    private void SetDatePickerLanguage()
    {
        // Get all DatePickers in the dialog
        var datePickers = FindVisualChildren<DatePicker>(this);

        // Set language based on current UI culture
        var culture = System.Threading.Thread.CurrentThread.CurrentUICulture;
        var xmlLanguage = XmlLanguage.GetLanguage(culture.Name);

        foreach (var datePicker in datePickers)
        {
            datePicker.Language = xmlLanguage;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);

            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (var childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }
}
