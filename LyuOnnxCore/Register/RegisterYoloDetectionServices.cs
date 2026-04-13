using LyuOnnxCore.Interfaces;
using LyuOnnxCore.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LyuOnnxCore.Register;

public static class RegisterYoloDetectionServices
{
    public static IServiceCollection AddYoloDetectionServices(this IServiceCollection services)
    {
        services.AddTransient<IYoloHbbDetectionService, YoloHbbDetectionService>();
        services.AddTransient<IYoloObbDetectionService, YoloObbDetectionService>();
        return services;
    }
}
