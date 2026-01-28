using MahTemp.Model;
using OpenCvSharp;

namespace MahTemp.Services;

public partial class CvService : ICvService
{
    public Mat? ApplySetting(Mat mat, CvSettings setting)
    {
        if (mat == null || mat.Empty())
            return mat;

        return setting.ProcessType switch
        {
            CvProcessType.ContourDetection => ApplyContourDetection(mat, setting),
            CvProcessType.None => mat,
            _ => mat
        };
    }

    /// <summary>
    /// 应用轮廓检测
    /// </summary>
    private Mat ApplyContourDetection(Mat sourceMat, CvSettings setting)
    {
        // 创建输出图像（彩色，用于绘制轮廓）
        Mat resultMat = sourceMat.Clone();
        Mat processedMat = sourceMat.Clone();

        try
        {
            // 1. 转换为灰度图（轮廓检测必须使用灰度图）
            if (processedMat.Channels() > 1)
            {
                processedMat = ConvertToGray(processedMat, setting.GrayMethod);
            }

            // 2. 高斯模糊（如果需要）
            if (setting.ApplyGaussianBlur)
            {
                int kernelSize = setting.GaussianBlurKernelSize;
                // 确保核大小为奇数
                if (kernelSize % 2 == 0)
                    kernelSize++;
                
                Cv2.GaussianBlur(processedMat, processedMat, new Size(kernelSize, kernelSize), 0);
            }

            // 3. 二值化（只能应用于灰度图）
            Cv2.Threshold(processedMat, processedMat, 
                setting.ThresholdValue, 
                setting.ThresholdMaxValue, 
                setting.ThresholdType);

            // 4. 查找轮廓
            Cv2.FindContours(processedMat, out Point[][] contours, out HierarchyIndex[] hierarchy,
                setting.RetrievalMode, setting.ApproximationMode);

            // 5. 过滤轮廓（根据最小面积）
            var filteredContours = contours
                .Where(contour => Cv2.ContourArea(contour) >= setting.MinContourArea)
                .ToArray();

            // 6. 绘制轮廓到原图
            for (int i = 0; i < filteredContours.Length; i++)
            {
                // 绘制轮廓
                Cv2.DrawContours(resultMat, filteredContours, i, 
                    setting.ContourColor, 
                    setting.ContourThickness);

                // 绘制轮廓索引（如果需要）
                if (setting.DrawContourIndex)
                {
                    var moments = Cv2.Moments(filteredContours[i]);
                    if (moments.M00 != 0)
                    {
                        int cx = (int)(moments.M10 / moments.M00);
                        int cy = (int)(moments.M01 / moments.M00);
                        Cv2.PutText(resultMat, i.ToString(), new Point(cx, cy),
                            HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 0), 2);
                    }
                }
            }

            return resultMat;
        }
        finally
        {
            processedMat?.Dispose();
        }
    }

    /// <summary>
    /// 根据指定方法转换为灰度图
    /// </summary>
    private Mat ConvertToGray(Mat sourceMat, GrayConversionMethod method)
    {
        Mat grayMat = new Mat();

        switch (method)
        {
            case GrayConversionMethod.BGR2GRAY:
                // 标准灰度转换 (0.299*R + 0.587*G + 0.114*B)
                Cv2.CvtColor(sourceMat, grayMat, ColorConversionCodes.BGR2GRAY);
                break;

            case GrayConversionMethod.BlueChannel:
                // 提取蓝色通道
                Cv2.ExtractChannel(sourceMat, grayMat, 0);
                break;

            case GrayConversionMethod.GreenChannel:
                // 提取绿色通道
                Cv2.ExtractChannel(sourceMat, grayMat, 1);
                break;

            case GrayConversionMethod.RedChannel:
                // 提取红色通道
                Cv2.ExtractChannel(sourceMat, grayMat, 2);
                break;

            case GrayConversionMethod.Average:
                // 平均值法
                Mat[] channels = Cv2.Split(sourceMat);
                grayMat = (channels[0] + channels[1] + channels[2]) / 3;
                foreach (var channel in channels)
                    channel.Dispose();
                break;

            case GrayConversionMethod.MaxValue:
                // 最大值法
                Mat[] channelsMax = Cv2.Split(sourceMat);
                grayMat = channelsMax[0].Clone();
                Cv2.Max(grayMat, channelsMax[1], grayMat);
                Cv2.Max(grayMat, channelsMax[2], grayMat);
                foreach (var channel in channelsMax)
                    channel.Dispose();
                break;

            case GrayConversionMethod.MinValue:
                // 最小值法
                Mat[] channelsMin = Cv2.Split(sourceMat);
                grayMat = channelsMin[0].Clone();
                Cv2.Min(grayMat, channelsMin[1], grayMat);
                Cv2.Min(grayMat, channelsMin[2], grayMat);
                foreach (var channel in channelsMin)
                    channel.Dispose();
                break;

            default:
                Cv2.CvtColor(sourceMat, grayMat, ColorConversionCodes.BGR2GRAY);
                break;
        }

        return grayMat;
    }
}
