using MahTemp.Model;
using OpenCvSharp;

namespace MahTemp.Services;

public partial class CvService : ICvService
{
    // 用于在步骤之间传递轮廓数据
    private Point[][]? _cachedContours;
    private FindContoursSettings? _cachedFindSettings;

    public Mat? ApplySetting(Mat mat, CvSettings setting)
    {
        if (mat == null || mat.Empty())
            return mat;

        // 重置缓存
        _cachedContours = null;
        _cachedFindSettings = null;

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
            resultMat = ApplyDrawContours(resultMat, setting.DrawContours, setting.FindContours);
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
        
        System.Diagnostics.Debug.WriteLine($"=== 二值化开始 ===");
        System.Diagnostics.Debug.WriteLine($"UseAdaptive: {setting.UseAdaptive}");
        System.Diagnostics.Debug.WriteLine($"输入图像: {sourceMat.Width}x{sourceMat.Height}, 通道数: {sourceMat.Channels()}");
        
        // 判断使用普通二值化还是自适应二值化
        if (setting.UseAdaptive)
        {
            // 自适应二值化
            int blockSize = setting.BlockSize;
            
            // 确保块大小为奇数且大于1
            if (blockSize % 2 == 0)
                blockSize++;
            if (blockSize < 3)
                blockSize = 3;
            
            System.Diagnostics.Debug.WriteLine($"执行自适应二值化: 方法={setting.AdaptiveMethod}, 类型={setting.AdaptiveType}, 块大小={blockSize}, C={setting.C}, 最大值={setting.MaxValue}");
            
            Cv2.AdaptiveThreshold(sourceMat, resultMat, setting.MaxValue,
                setting.AdaptiveMethod, setting.AdaptiveType, blockSize, setting.C);
            
            System.Diagnostics.Debug.WriteLine($"自适应二值化完成");
        }
        else
        {
            // 普通二值化
            System.Diagnostics.Debug.WriteLine($"执行普通二值化: 类型={setting.Type}, 阈值={setting.ThresholdValue}, 最大值={setting.MaxValue}");
            
            double actualThreshold = Cv2.Threshold(sourceMat, resultMat, setting.ThresholdValue, 
                setting.MaxValue, setting.Type);
            
            // 如果是自动阈值方法（Otsu 或 Triangle），输出实际计算的阈值
            if (setting.Type == ThresholdTypes.Otsu || setting.Type == ThresholdTypes.Triangle)
            {
                System.Diagnostics.Debug.WriteLine($"自动计算的阈值: {actualThreshold:F2}");
            }
            
            System.Diagnostics.Debug.WriteLine($"普通二值化完成");
        }
        
        System.Diagnostics.Debug.WriteLine($"输出图像: {resultMat.Width}x{resultMat.Height}, 通道数: {resultMat.Channels()}");
        System.Diagnostics.Debug.WriteLine($"=== 二值化结束 ===\n");

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

        // 缓存轮廓数据供绘制步骤使用
        _cachedContours = filteredContours;
        _cachedFindSettings = setting;

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
    private Mat ApplyDrawContours(Mat sourceMat, DrawContoursSettings setting, FindContoursSettings? findSetting)
    {
        Mat resultMat;
        Point[][] contoursToUse;

        // 如果有缓存的轮廓数据，使用缓存
        if (_cachedContours != null && _cachedContours.Length > 0)
        {
            contoursToUse = _cachedContours;
            
            // 如果原图是灰度图，转换为彩色以便绘制彩色轮廓
            if (sourceMat.Channels() == 1)
            {
                resultMat = new Mat();
                Cv2.CvtColor(sourceMat, resultMat, ColorConversionCodes.GRAY2BGR);
            }
            else
            {
                resultMat = sourceMat.Clone();
            }
        }
        else
        {
            // 如果没有缓存，需要重新查找轮廓
            if (sourceMat.Channels() == 1)
            {
                // 使用 FindContours 的设置（如果有），否则使用默认值
                var retrievalMode = findSetting?.RetrievalMode ?? RetrievalModes.External;
                var approxMode = findSetting?.ApproximationMode ?? ContourApproximationModes.ApproxSimple;
                
                Cv2.FindContours(sourceMat.Clone(), out Point[][] contours, out HierarchyIndex[] hierarchy,
                    retrievalMode, approxMode);

                contoursToUse = contours;
                
                // 转换为彩色
                resultMat = new Mat();
                Cv2.CvtColor(sourceMat, resultMat, ColorConversionCodes.GRAY2BGR);
            }
            else
            {
                // 彩色图无法直接查找轮廓
                return sourceMat;
            }
        }

        // 绘制轮廓
        if (setting.ContourIndex == -1)
        {
            // 绘制所有轮廓
            for (int i = 0; i < contoursToUse.Length; i++)
            {
                Cv2.DrawContours(resultMat, contoursToUse, i, setting.ContourColor, setting.Thickness);

                // 绘制索引
                if (setting.DrawIndex)
                {
                    var moments = Cv2.Moments(contoursToUse[i]);
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
            if (setting.ContourIndex >= 0 && setting.ContourIndex < contoursToUse.Length)
            {
                Cv2.DrawContours(resultMat, contoursToUse, setting.ContourIndex, 
                    setting.ContourColor, setting.Thickness);
            }
        }

        return resultMat;
    }
}
