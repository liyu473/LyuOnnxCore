using System.Windows.Controls;
using MahTemp.ViewModels;

namespace MahTemp.Views;

public partial class DetectionPage : UserControl
{
    public DetectionPage(DetectionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
