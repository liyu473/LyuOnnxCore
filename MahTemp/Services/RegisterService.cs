using MahTemp.ViewModels;
using MahTemp.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MahTemp.Services;

public static class RegisterService
{
    public static void RegisterViews(this IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<DetectionPage>();
    }


    public static void RegisterViewModels(this IServiceCollection services)
    {
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<DetectionViewModel>();
    }
}
