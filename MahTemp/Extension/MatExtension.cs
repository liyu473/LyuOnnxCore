using MahTemp.Model;
using MahTemp.Services;
using OpenCvSharp;

namespace MahTemp.Extension;

public static class MatExtension
{
    private static ICvService CvService => App.GetService<ICvService>();

    public static Mat? GetResult(this Mat mat, CvSettings? setting)
    {
        if(setting is null) return mat;
        return CvService.ApplySetting(mat, setting);
    }
}
