using CommunityToolkit.Mvvm.ComponentModel;
using ControlzEx.Theming;
using System.Windows;

namespace MahTemp.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string CurrentTheme { get; set; } = "Light";

    [ObservableProperty]
    public partial string CurrentAccent { get; set; } = "Blue";

    [ObservableProperty]
    public partial WindowBackdropType CurrentBackdrop { get; set; } = WindowBackdropType.Mica;

    public string[] Themes { get; } = ["Light", "Dark"];

    public string[] Accents { get; } =
    [
        "Red", "Green", "Blue", "Purple", "Orange", "Lime", "Emerald",
        "Teal", "Cyan", "Cobalt", "Indigo", "Violet", "Pink", "Magenta",
        "Crimson", "Amber", "Yellow", "Brown", "Olive", "Steel", "Mauve", "Taupe", "Sienna"
    ];

    public WindowBackdropType[] Backdrops { get; } =
    [
        WindowBackdropType.Auto,
        WindowBackdropType.None,
        WindowBackdropType.Mica,
        WindowBackdropType.Acrylic,
        WindowBackdropType.Tabbed
    ];

    public SettingsViewModel()
    {
        var theme = ThemeManager.Current.DetectTheme(Application.Current);
        if (theme != null)
        {
            CurrentTheme = theme.BaseColorScheme;
            CurrentAccent = theme.ColorScheme;
        }
    }

    partial void OnCurrentThemeChanged(string value) => ApplyTheme();

    partial void OnCurrentAccentChanged(string value) => ApplyTheme();

    partial void OnCurrentBackdropChanged(WindowBackdropType value)
    {
        if (Application.Current.MainWindow != null)
        {
            WindowBackdropManager.SetBackdropType(Application.Current.MainWindow, value);
        }
    }

    private void ApplyTheme()
    {
        ThemeManager.Current.ChangeTheme(Application.Current, $"{CurrentTheme}.{CurrentAccent}");
    }
}
