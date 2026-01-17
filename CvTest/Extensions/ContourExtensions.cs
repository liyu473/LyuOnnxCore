using OpenCvSharp;
using LyuCvExCore.Models;

namespace LyuCvExCore.Extensions;

/// <summary>
/// 轮廓检测扩展方法
/// </summary>
public static class ContourExtensions
{
    /// <summary>
    /// 检测图像中的轮廓
    /// </summary>
    /// <param name="mat">输入图像（彩色或灰度）</param>
    /// <param name="options">轮廓检测选项</param>
    /// <returns>检测到的轮廓列表</returns>
    public static Point[][] FindContours(this Mat mat, ContourOptions? options = null)
    {
        options ??= new ContourOptions();

        using var gray = mat.Channels() == 1 ? mat.Clone() : mat.CvtColor(ColorConversionCodes.BGR2GRAY);

        // 预处理
        using var processed = PreprocessForContour(gray, options);

        // 查找轮廓
        Cv2.FindContours(processed, out var contours, out _, options.Mode, options.Method);

        // 过滤轮廓
        var filtered = FilterContours(contours, options);

        return filtered;
    }

    /// <summary>
    /// 检测轮廓并返回详细信息
    /// </summary>
    /// <param name="mat">输入图像</param>
    /// <param name="options">轮廓检测选项</param>
    /// <returns>轮廓信息列表</returns>
    public static List<ContourInfo> FindContourInfos(this Mat mat, ContourOptions? options = null)
    {
        options ??= new ContourOptions();

        using var gray = mat.Channels() == 1 ? mat.Clone() : mat.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var processed = PreprocessForContour(gray, options);

        Cv2.FindContours(processed, out var contours, out var hierarchy, options.Mode, options.Method);

        var result = new List<ContourInfo>();

        for (int i = 0; i < contours.Length; i++)
        {
            var contour = contours[i];
            double area = Cv2.ContourArea(contour);
            double perimeter = Cv2.ArcLength(contour, true);

            // 过滤
            if (area < options.MinArea || area > options.MaxArea)
                continue;
            if (perimeter < options.MinPerimeter || perimeter > options.MaxPerimeter)
                continue;

            var boundingRect = Cv2.BoundingRect(contour);
            var minAreaRect = Cv2.MinAreaRect(contour);
            var moments = Cv2.Moments(contour);

            // 计算圆形度
            double circularity = perimeter > 0 ? 4 * Math.PI * area / (perimeter * perimeter) : 0;

            // 计算质心
            Point2f centroid = moments.M00 > 0
                ? new Point2f((float)(moments.M10 / moments.M00), (float)(moments.M01 / moments.M00))
                : new Point2f(boundingRect.X + boundingRect.Width / 2f, boundingRect.Y + boundingRect.Height / 2f);

            // 计算凸包
            var hull = Cv2.ConvexHull(contour);
            double hullArea = Cv2.ContourArea(hull);
            double solidity = hullArea > 0 ? area / hullArea : 0;

            result.Add(new ContourInfo
            {
                Points = contour,
                Area = area,
                Perimeter = perimeter,
                BoundingRect = boundingRect,
                MinAreaRect = minAreaRect,
                Centroid = centroid,
                Circularity = circularity,
                Solidity = solidity,
                ConvexHull = hull,
                HierarchyIndex = i
            });
        }

        return result;
    }

    /// <summary>
    /// 在图像上绘制轮廓
    /// </summary>
    /// <param name="mat">输入图像</param>
    /// <param name="contours">轮廓点数组</param>
    /// <param name="color">轮廓颜色 (B, G, R)</param>
    /// <param name="thickness">线条粗细，-1 表示填充</param>
    /// <returns>绘制后的图像</returns>
    public static Mat DrawContours(this Mat mat, Point[][] contours, Scalar? color = null, int thickness = 2)
    {
        var result = mat.Clone();
        color ??= new Scalar(0, 255, 0);
        Cv2.DrawContours(result, contours, -1, color.Value, thickness);
        return result;
    }

    /// <summary>
    /// 在图像上绘制轮廓信息（包含边界框）
    /// </summary>
    /// <param name="mat">输入图像</param>
    /// <param name="contourInfos">轮廓信息列表</param>
    /// <param name="drawOptions">绘制选项</param>
    /// <returns>绘制后的图像</returns>
    public static Mat DrawContourInfos(this Mat mat, List<ContourInfo> contourInfos, ContourDrawOptions? drawOptions = null)
    {
        drawOptions ??= new ContourDrawOptions();
        var result = mat.Clone();

        foreach (var info in contourInfos)
        {
            // 绘制轮廓
            if (drawOptions.DrawContour)
            {
                Cv2.DrawContours(result, [info.Points], 0, drawOptions.ContourColor, drawOptions.ContourThickness);
            }

            // 绘制边界矩形
            if (drawOptions.DrawBoundingRect)
            {
                Cv2.Rectangle(result, info.BoundingRect, drawOptions.BoundingRectColor, drawOptions.BoundingRectThickness);
            }

            // 绘制最小外接矩形
            if (drawOptions.DrawMinAreaRect)
            {
                var points = info.MinAreaRect.Points();
                for (int i = 0; i < 4; i++)
                {
                    Cv2.Line(result, (Point)points[i], (Point)points[(i + 1) % 4], drawOptions.MinAreaRectColor, drawOptions.MinAreaRectThickness);
                }
            }

            // 绘制质心
            if (drawOptions.DrawCentroid)
            {
                Cv2.Circle(result, (Point)info.Centroid, 4, drawOptions.CentroidColor, -1);
            }

            // 绘制凸包
            if (drawOptions.DrawConvexHull)
            {
                Cv2.Polylines(result, [info.ConvexHull], true, drawOptions.ConvexHullColor, drawOptions.ConvexHullThickness);
            }
        }

        return result;
    }

    /// <summary>
    /// 获取最大轮廓
    /// </summary>
    /// <param name="contours">轮廓数组</param>
    /// <returns>最大轮廓，如果没有轮廓返回 null</returns>
    public static Point[]? GetLargestContour(this Point[][] contours)
    {
        if (contours == null || contours.Length == 0)
            return null;

        return contours.OrderByDescending(c => Cv2.ContourArea(c)).FirstOrDefault();
    }

    /// <summary>
    /// 获取最大轮廓信息
    /// </summary>
    /// <param name="contourInfos">轮廓信息列表</param>
    /// <returns>最大轮廓信息</returns>
    public static ContourInfo? GetLargestContour(this List<ContourInfo> contourInfos)
    {
        return contourInfos.OrderByDescending(c => c.Area).FirstOrDefault();
    }

    /// <summary>
    /// 过滤边缘平滑的轮廓，去除坑坑洼洼的边缘
    /// </summary>
    /// <param name="contourInfos">轮廓信息列表</param>
    /// <param name="smoothness">平滑度阈值 (0-1)，值越大要求越平滑。推荐值：0.85-0.95</param>
    /// <returns>过滤后的轮廓信息列表</returns>
    public static List<ContourInfo> FilterSmooth(
        this List<ContourInfo> contourInfos,
        double smoothness = 0.9)
    {
        if (contourInfos == null || contourInfos.Count == 0)
            return [];

        if (smoothness < 0 || smoothness > 1)
            throw new ArgumentOutOfRangeException(nameof(smoothness), "平滑度阈值必须在 0-1 之间");

        var result = new List<ContourInfo>();

        foreach (var info in contourInfos)
        {
            // 使用实心度 (Solidity) 作为主要判断标准
            // 实心度 = 轮廓面积 / 凸包面积
            // 越接近 1 表示轮廓越平滑，越接近凸包
            if (info.Solidity >= smoothness)
            {
                result.Add(info);
            }
        }

        return result;
    }

    /// <summary>
    /// 过滤边缘平滑的轮廓（高级版本，支持多种平滑度指标）
    /// </summary>
    /// <param name="contourInfos">轮廓信息列表</param>
    /// <param name="options">平滑度过滤选项</param>
    /// <returns>过滤后的轮廓信息列表</returns>
    public static List<ContourInfo> FilterSmooth(
        this List<ContourInfo> contourInfos,
        SmoothFilterOptions options)
    {
        if (contourInfos == null || contourInfos.Count == 0)
            return [];

        var result = new List<ContourInfo>();

        foreach (var info in contourInfos)
        {
            bool isSmooth = true;

            // 1. 实心度检查（最重要的指标）
            if (options.UseSolidity && info.Solidity < options.MinSolidity)
            {
                isSmooth = false;
            }

            // 2. 圆形度检查（可选）
            if (isSmooth && options.UseCircularity && info.Circularity < options.MinCircularity)
            {
                isSmooth = false;
            }

            // 3. 凸包偏差检查（可选）
            if (isSmooth && options.UseConvexityDefect)
            {
                double convexityDefect = CalculateConvexityDefect(info);
                if (convexityDefect > options.MaxConvexityDefect)
                {
                    isSmooth = false;
                }
            }

            if (isSmooth)
            {
                result.Add(info);
            }
        }

        return result;
    }

    #region 私有方法

    /// <summary>
    /// 轮廓检测预处理
    /// </summary>
    private static Mat PreprocessForContour(Mat gray, ContourOptions options)
    {
        var result = gray.Clone();

        // 高斯模糊
        if (options.GaussianBlurSize > 0)
        {
            int size = options.GaussianBlurSize % 2 == 0 ? options.GaussianBlurSize + 1 : options.GaussianBlurSize;
            Cv2.GaussianBlur(result, result, new Size(size, size), 0);
        }

        // 二值化
        switch (options.ThresholdType)
        {
            case ContourThresholdType.Binary:
                Cv2.Threshold(result, result, options.ThresholdValue, 255, ThresholdTypes.Binary);
                break;
            case ContourThresholdType.BinaryInv:
                Cv2.Threshold(result, result, options.ThresholdValue, 255, ThresholdTypes.BinaryInv);
                break;
            case ContourThresholdType.Otsu:
                Cv2.Threshold(result, result, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                break;
            case ContourThresholdType.OtsuInv:
                Cv2.Threshold(result, result, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
                break;
            case ContourThresholdType.Adaptive:
                Cv2.AdaptiveThreshold(result, result, 255, AdaptiveThresholdTypes.GaussianC,
                    ThresholdTypes.Binary, options.AdaptiveBlockSize, options.AdaptiveC);
                break;
            case ContourThresholdType.AdaptiveInv:
                Cv2.AdaptiveThreshold(result, result, 255, AdaptiveThresholdTypes.GaussianC,
                    ThresholdTypes.BinaryInv, options.AdaptiveBlockSize, options.AdaptiveC);
                break;
            case ContourThresholdType.Canny:
                Cv2.Canny(result, result, options.CannyThreshold1, options.CannyThreshold2);
                break;
        }

        // 形态学操作
        if (options.MorphologyOperation != MorphTypes.HitMiss)
        {
            using var kernel = Cv2.GetStructuringElement(options.MorphologyShape,
                new Size(options.MorphologyKernelSize, options.MorphologyKernelSize));
            Cv2.MorphologyEx(result, result, options.MorphologyOperation, kernel, iterations: options.MorphologyIterations);
        }

        return result;
    }

    /// <summary>
    /// 过滤轮廓
    /// </summary>
    private static Point[][] FilterContours(Point[][] contours, ContourOptions options)
    {
        var result = new List<Point[]>();

        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            double perimeter = Cv2.ArcLength(contour, true);

            if (area < options.MinArea || area > options.MaxArea)
                continue;
            if (perimeter < options.MinPerimeter || perimeter > options.MaxPerimeter)
                continue;

            result.Add(contour);
        }

        return [.. result];
    }


    /// <summary>
    /// 计算轮廓的凸性缺陷程度
    /// </summary>
    /// <param name="info">轮廓信息</param>
    /// <returns>凸性缺陷程度 (0-1)，值越大表示边缘越不平滑</returns>
    private static double CalculateConvexityDefect(ContourInfo info)
    {
        if (info.Area <= 0)
            return 1.0;

        // 计算凸包周长与轮廓周长的比值
        double hullPerimeter = Cv2.ArcLength(info.ConvexHull, true);
        if (hullPerimeter <= 0)
            return 1.0;

        // 周长差异比例
        double perimeterRatio = Math.Abs(info.Perimeter - hullPerimeter) / hullPerimeter;

        // 面积差异比例（已经通过 Solidity 体现）
        // 综合评估：周长差异越大，说明边缘越不平滑
        return Math.Min(perimeterRatio, 1.0);
    }

    #endregion
}
