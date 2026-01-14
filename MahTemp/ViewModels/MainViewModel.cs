using CommunityToolkit.Mvvm.ComponentModel;
using MahTemp.Views;
using System.Reflection;

namespace MahTemp.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public string AppName => Assembly.GetExecutingAssembly().GetName().Name ?? string.Empty;

    public string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;

    [ObservableProperty]
    public partial object View { get; set; } = App.GetService<HomePage>();
}
