using MahTemp.Model;
using OpenCvSharp;

namespace MahTemp.Services;

public partial class CvService : ICvService
{
    public Mat? ApplySetting(Mat mat, CvSettings setting)
    {
        if (mat == null || mat.Empty())
            return mat;

        Mat resultMat = mat;

        // 1. 灰度化（如果启用）
        if (setting.IsApplyGrayscale && resultMat.Channels() > 1)
        {
            resultMat = ApplyGrayscale(resultMat);
        }

        // 2. 高斯模糊（如果启用）
        if (setting.GaussianBlur?.IsEnabled == true)
        {
            resultMat = ApplyGaussianBlur(resultMat, setting.GaussianBlur);
        }

        // 3. 二值化（如果启用）
        if (setting.Threshold?.IsEnabled == true)
        {
            resultMat = ApplyThreshold(resultMat, setting.Threshold);
        }

        // 4. 查找轮廓（如果启用）
        if (setting.FindContours?.IsEnabled == true)
        {
            resultMat = ApplyFindContours(resultMat, setting.FindContours);
        }

        // 5. 绘制轮廓（如果启用）
        if (setting.DrawContours?.IsEnabled == true)
        {
            resultMat = ApplyDrawContours(resultMat, setting.DrawContours);
        }

        return resultMat;
    }

    /// <summary>
    /// 应用灰度化（标准BGR2GRAY方法）
    /// </summary>
    private Mat ApplyGrayscale(Mat sourceMat)
    {
        if (sourceMat.Channels() == 1)
            return sourceMat;

        Mat grayMat = new Mat();
        Cv2.CvtColor(sourceMat, grayMat, ColorConversionCodes.BGR2GRAY);
        return grayMat;
    }

    /// <summary>
    /// 应用高斯模糊
    /// </summary>
    private Mat ApplyGaussianBlur(Mat sourceMat, GaussianBlurSettings setting)
    {
        Mat resultMat = new Mat();
        int kernelSize = setting.KernelSize;
        
        // 确保核大小为奇数
        if (kernelSize % 2 == 0)
            kernelSize++;

        Cv2.GaussianBlur(sourceMat, resultMat, new Size(kernelSize, kernelSize), 
            setting.SigmaX, setting.SigmaY);

        return resultMat;
    }

    /// <summary>
    /// 应用二值化
    /// </summary>
    private Mat ApplyThreshold(Mat sourceMat, ThresholdSettings setting)
    {
        Mat resultMat = new Mat();
        Cv2.Threshold(sourceMat, resultMat, setting.ThresholdValue, 
            setting.MaxValue, setting.Type);

        return resultMat;
    }

    /// <summary>
    /// 应用查找轮廓（返回二值图，轮廓用白色显示）
    /// </summary>
    private Mat ApplyFindContours(Mat sourceMat, FindContoursSettings setting)
    {
        // 查找轮廓需要二值图
        if (sourceMat.Channels() > 1)
        {
            throw new InvalidOperationException("FindContours requires a binary (grayscale) image. Please enable Grayscale and Threshold first.");
        }

        // 查找轮廓
        Cv2.FindContours(sourceMat, out Point[][] contours, out HierarchyIndex[] hierarchy,
            setting.RetrievalMode, setting.ApproximationMode);

        // 过滤轮廓
        var filteredContours = contours
            .Where(contour => Cv2.ContourArea(contour) >= setting.MinContourArea)
            .ToArray();

        // 创建空白图像来显示找到的轮廓
        Mat resultMat = Mat.Zeros(sourceMat.Size(), MatType.CV_8UC1);
        
        // 在结果图上绘制轮廓（白色）以便可视化
        for (int i = 0; i < filteredContours.Length; i++)
        {
            Cv2.DrawContours(resultMat, filteredContours, i, Scalar.White, 1);
        }

        return resultMat;
    }

    /// <summary>
    /// 应用绘制轮廓
    /// </summary>
    private Mat ApplyDrawContours(Mat sourceMat, DrawContoursSettings setting)
    {
        Mat resultMat = sourceMat.Clone();
        
        // 如果是灰度图，需要先查找轮廓
        if (sourceMat.Channels() == 1)
        {
            Cv2.FindContours(sourceMat.Clone(), out Point[][] contours, out HierarchyIndex[] hierarchy,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            // 如果原图是灰度图，转换为彩色以便绘制彩色轮廓
            resultMat = new Mat();
            Cv2.CvtColor(sourceMat, resultMat, ColorConversionCodes.GRAY2BGR);

            // 绘制轮廓
            if (setting.ContourIndex == -1)
            {
                // 绘制所有轮廓
                for (int i = 0; i < contours.Length; i++)
                {
                    Cv2.DrawContours(resultMat, contours, i, setting.ContourColor, setting.Thickness);

                    // 绘制索引
                    if (setting.DrawIndex)
                    {
                        var moments = Cv2.Moments(contours[i]);
                        if (moments.M00 != 0)
                        {
                            int cx = (int)(moments.M10 / moments.M00);
                            int cy = (int)(moments.M01 / moments.M00);
                            Cv2.PutText(resultMat, i.ToString(), new Point(cx, cy),
                                HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 0), 2);
                        }
                    }
                }
            }
            else
            {
                // 绘制指定索引的轮廓
                if (setting.ContourIndex >= 0 && setting.ContourIndex < contours.Length)
                {
                    Cv2.DrawContours(resultMat, contours, setting.ContourIndex, 
                        setting.ContourColor, setting.Thickness);
                }
            }
        }

        return resultMat;
    }
}
