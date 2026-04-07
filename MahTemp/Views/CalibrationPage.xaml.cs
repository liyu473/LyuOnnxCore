namespace MahTemp.Views;

public partial class CalibrationPage
{
    public CalibrationPage()
    {
        InitializeComponent();
        DataContext = App.GetService<ViewModels.CalibrationViewModel>();
    }
}
