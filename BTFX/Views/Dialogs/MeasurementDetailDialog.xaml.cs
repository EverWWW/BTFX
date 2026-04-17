using System.Windows;
using MaterialDesignThemes.Wpf;
using BTFX.ViewModels;

namespace BTFX.Views.Dialogs;

/// <summary>
/// MeasurementDetailDialog.xaml 的交互逻辑
/// </summary>
public partial class MeasurementDetailDialog
{
    public MeasurementDetailDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MeasurementDetailViewModel oldVm)
        {
            oldVm.CloseRequested -= OnCloseRequested;
        }

        if (e.NewValue is MeasurementDetailViewModel newVm)
        {
            newVm.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested()
    {
        DialogHost.Close("RootDialog");
    }
}
