using MahTemp.Model;
using MahTemp.Services;
using OpenCvSharp;

namespace MahTemp.Extension;

public static class MatExtension
{
    private static ICvService CvService => App.GetService<ICvService>();

    public static Mat? GetResult(this Mat mat, CvSettings setting)
    {
        return CvService.ApplySetting(mat, setting);
    }
}
