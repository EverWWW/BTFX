using System.Windows.Controls;
using System.Windows;
using BTFX.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace BTFX.Views;

/// <summary>
/// PatientSelectionView.xaml interaction logic
/// </summary>
public partial class PatientSelectionView : UserControl
{
    /// <summary>
    /// Constructor
    /// </summary>
    public PatientSelectionView()
    {
        InitializeComponent();

        // Get ViewModel from DI container
        DataContext = App.Services.GetRequiredService<PatientSelectionViewModel>();

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdatePageSizeFromViewport();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Math.Abs(e.PreviousSize.Height - e.NewSize.Height) < 0.5)
        {
            return;
        }

        UpdatePageSizeFromViewport();
    }

    /// <summary>
    /// 根据列表可视区域高度更新分页大小。
    /// </summary>
    private void UpdatePageSizeFromViewport()
    {
        if (DataContext is not PatientSelectionViewModel viewModel)
        {
            return;
        }

        var viewportHeight = PatientListViewport?.ActualHeight ?? 0;
        if (viewportHeight <= 0)
        {
            return;
        }

        viewModel.UpdatePageSize(viewportHeight);
    }
}
