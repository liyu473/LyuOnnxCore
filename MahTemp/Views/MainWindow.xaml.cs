using MahApps.Metro.Controls;
using MahTemp.ViewModels;

namespace MahTemp.Views;

public partial class MainWindow : MetroWindow
{
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        ControlsHelper.SetCornerRadius(this, new System.Windows.CornerRadius(8));
    }

    private void HamburgerMenuControl_OnItemInvoked(object sender, HamburgerMenuItemInvokedEventArgs args)
    {
        HamburgerMenuControl.Content = args.InvokedItem;
    }
}
