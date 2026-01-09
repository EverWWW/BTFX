using System.Windows;
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

    protected override void OnClosed(EventArgs e)
    {
        if (DataContext is PatientEditViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }
        base.OnClosed(e);
    }
}
