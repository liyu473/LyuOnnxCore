using MahTemp.Model;
using OpenCvSharp;

namespace MahTemp.Services;

public partial class CvService : ICvService
{
    public Mat? ApplySetting(Mat mat, CvSettings setting)
    {
        return mat;
    }
}
