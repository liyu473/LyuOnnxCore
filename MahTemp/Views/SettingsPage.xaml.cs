using System.Windows.Controls;
using MahTemp.ViewModels;

namespace MahTemp.Views;

public partial class SettingsPage : UserControl
{
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
