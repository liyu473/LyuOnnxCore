using MahTemp.ViewModels;
using System.Windows.Controls;

namespace MahTemp.Views;

/// <summary>
/// OpenCvView.xaml 的交互逻辑
/// </summary>
public partial class OpenCvView : UserControl
{
    public OpenCvView(OpenCvViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
