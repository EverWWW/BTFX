using System.Windows.Controls;
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
    }
}
