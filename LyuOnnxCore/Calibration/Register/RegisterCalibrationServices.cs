using LyuOnnxCore.Calibration.Interface;
using LyuOnnxCore.Calibration.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LyuOnnxCore.Calibration.Register;

public static class RegisterCalibrationServices
{
    public static IServiceCollection AddCalibrationServices(this IServiceCollection services)
    {
        services.AddTransient<ICameraCalibration, CameraCalibrationService>();
        services.AddTransient<INinePointCalibration, NinePointCalibrationService>();
        services.AddTransient<IAxisPositionCompensation, AxisPositionCompensationService>();
        return services;
    }
}
