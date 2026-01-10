using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using BTFX.Services.Interfaces;
using BTFX.ViewModels;

namespace BTFX.Views.Dialogs;

/// <summary>
/// PatientEditDialog.xaml Interaction Logic
/// </summary>
public partial class PatientEditDialog : Window
{
    public PatientEditDialog()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PatientEditViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        // Set DatePicker language based on current culture
        SetDatePickerLanguage();

        // Subscribe to language changes if localization service is available
        if (App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
        {
            localizationService.LanguageChanged += OnLanguageChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PatientEditViewModel.ShouldClose))
        {
            if (DataContext is PatientEditViewModel viewModel && viewModel.ShouldClose)
            {
                DialogResult = viewModel.DialogResult;
                Close();
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

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is PatientEditViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        // Unsubscribe from language changes
        if (App.Services?.GetService(typeof(ILocalizationService)) is ILocalizationService localizationService)
        {
            localizationService.LanguageChanged -= OnLanguageChanged;
        }

        base.OnClosed(e);
    }
}
