using LyuOnnxCore.Interfaces;
using LyuOnnxCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LyuOnnxCore.Register;

public static class RegisterDetectionServices
{
    public static IServiceCollection AddOnnxDetectionServices(this IServiceCollection services)
    {
        services.AddYoloHbbDetectionService();
        services.AddYoloObbDetectionService();
        services.AddYoloXHbbDetectionService();
        return services;
    }

    public static IServiceCollection AddYoloHbbDetectionService(
        this IServiceCollection services
    )
    {
        services.AddTransient<IYoloHbbDetectionService, YoloHbbDetectionService>();
        return services;
    }

    public static IServiceCollection AddYoloObbDetectionService(
        this IServiceCollection services
    )
    {
        services.AddTransient<IYoloObbDetectionService, YoloObbDetectionService>();
        return services;
    }

    public static IServiceCollection AddYoloXHbbDetectionService(
        this IServiceCollection services
    )
    {
        services.AddTransient<IYoloXHbbDetectionService, YoloXHbbDetectionService>();
        return services;
    }
}
