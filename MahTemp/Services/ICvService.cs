using MahTemp.Model;
using OpenCvSharp;

namespace MahTemp.Services;

public interface ICvService
{
    Mat? ApplySetting(Mat mat, CvSettings setting);
}
