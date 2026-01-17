using OpenCvSharp;

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

    #endregion
}

/// <summary>
/// 轮廓检测选项
/// </summary>
public class ContourOptions
{
    /// <summary>
    /// 轮廓检索模式
    /// </summary>
    public RetrievalModes Mode { get; set; } = RetrievalModes.External;

    /// <summary>
    /// 轮廓近似方法
    /// </summary>
    public ContourApproximationModes Method { get; set; } = ContourApproximationModes.ApproxSimple;

    /// <summary>
    /// 高斯模糊核大小（0 表示不模糊）
    /// </summary>
    public int GaussianBlurSize { get; set; } = 5;

    /// <summary>
    /// 二值化类型
    /// </summary>
    public ContourThresholdType ThresholdType { get; set; } = ContourThresholdType.Otsu;

    /// <summary>
    /// 二值化阈值（用于 Binary/BinaryInv）
    /// </summary>
    public double ThresholdValue { get; set; } = 127;

    /// <summary>
    /// 自适应阈值块大小
    /// </summary>
    public int AdaptiveBlockSize { get; set; } = 11;

    /// <summary>
    /// 自适应阈值常数
    /// </summary>
    public double AdaptiveC { get; set; } = 2;

    /// <summary>
    /// Canny 边缘检测阈值1
    /// </summary>
    public double CannyThreshold1 { get; set; } = 50;

    /// <summary>
    /// Canny 边缘检测阈值2
    /// </summary>
    public double CannyThreshold2 { get; set; } = 150;

    /// <summary>
    /// 形态学操作类型（HitMiss 表示不进行形态学操作）
    /// </summary>
    public MorphTypes MorphologyOperation { get; set; } = MorphTypes.HitMiss;

    /// <summary>
    /// 形态学核形状
    /// </summary>
    public MorphShapes MorphologyShape { get; set; } = MorphShapes.Rect;

    /// <summary>
    /// 形态学核大小
    /// </summary>
    public int MorphologyKernelSize { get; set; } = 3;

    /// <summary>
    /// 形态学操作迭代次数
    /// </summary>
    public int MorphologyIterations { get; set; } = 1;

    /// <summary>
    /// 最小轮廓面积
    /// </summary>
    public double MinArea { get; set; } = 0;

    /// <summary>
    /// 最大轮廓面积
    /// </summary>
    public double MaxArea { get; set; } = double.MaxValue;

    /// <summary>
    /// 最小轮廓周长
    /// </summary>
    public double MinPerimeter { get; set; } = 0;

    /// <summary>
    /// 最大轮廓周长
    /// </summary>
    public double MaxPerimeter { get; set; } = double.MaxValue;
}

/// <summary>
/// 轮廓二值化类型
/// </summary>
public enum ContourThresholdType
{
    /// <summary>
    /// 固定阈值二值化
    /// </summary>
    Binary,

    /// <summary>
    /// 固定阈值反向二值化
    /// </summary>
    BinaryInv,

    /// <summary>
    /// Otsu 自动阈值
    /// </summary>
    Otsu,

    /// <summary>
    /// Otsu 自动阈值（反向）
    /// </summary>
    OtsuInv,

    /// <summary>
    /// 自适应阈值
    /// </summary>
    Adaptive,

    /// <summary>
    /// 自适应阈值（反向）
    /// </summary>
    AdaptiveInv,

    /// <summary>
    /// Canny 边缘检测
    /// </summary>
    Canny
}

/// <summary>
/// 轮廓信息
/// </summary>
public class ContourInfo
{
    /// <summary>
    /// 轮廓点
    /// </summary>
    public Point[] Points { get; set; } = [];

    /// <summary>
    /// 轮廓面积
    /// </summary>
    public double Area { get; set; }

    /// <summary>
    /// 轮廓周长
    /// </summary>
    public double Perimeter { get; set; }

    /// <summary>
    /// 外接矩形
    /// </summary>
    public Rect BoundingRect { get; set; }

    /// <summary>
    /// 最小外接旋转矩形
    /// </summary>
    public RotatedRect MinAreaRect { get; set; }

    /// <summary>
    /// 质心
    /// </summary>
    public Point2f Centroid { get; set; }

    /// <summary>
    /// 圆形度 (0-1)，1 表示完美圆形
    /// </summary>
    public double Circularity { get; set; }

    /// <summary>
    /// 实心度 (0-1)，轮廓面积与凸包面积的比值
    /// </summary>
    public double Solidity { get; set; }

    /// <summary>
    /// 凸包点
    /// </summary>
    public Point[] ConvexHull { get; set; } = [];

    /// <summary>
    /// 层级索引
    /// </summary>
    public int HierarchyIndex { get; set; }
}

/// <summary>
/// 轮廓绘制选项
/// </summary>
public class ContourDrawOptions
{
    /// <summary>
    /// 是否绘制轮廓
    /// </summary>
    public bool DrawContour { get; set; } = true;

    /// <summary>
    /// 轮廓颜色
    /// </summary>
    public Scalar ContourColor { get; set; } = new Scalar(0, 255, 0);

    /// <summary>
    /// 轮廓线条粗细
    /// </summary>
    public int ContourThickness { get; set; } = 2;

    /// <summary>
    /// 是否绘制外接矩形
    /// </summary>
    public bool DrawBoundingRect { get; set; } = false;

    /// <summary>
    /// 外接矩形颜色
    /// </summary>
    public Scalar BoundingRectColor { get; set; } = new Scalar(255, 0, 0);

    /// <summary>
    /// 外接矩形线条粗细
    /// </summary>
    public int BoundingRectThickness { get; set; } = 2;

    /// <summary>
    /// 是否绘制最小外接矩形
    /// </summary>
    public bool DrawMinAreaRect { get; set; } = false;

    /// <summary>
    /// 最小外接矩形颜色
    /// </summary>
    public Scalar MinAreaRectColor { get; set; } = new Scalar(0, 0, 255);

    /// <summary>
    /// 最小外接矩形线条粗细
    /// </summary>
    public int MinAreaRectThickness { get; set; } = 2;

    /// <summary>
    /// 是否绘制质心
    /// </summary>
    public bool DrawCentroid { get; set; } = false;

    /// <summary>
    /// 质心颜色
    /// </summary>
    public Scalar CentroidColor { get; set; } = new Scalar(0, 255, 255);

    /// <summary>
    /// 是否绘制凸包
    /// </summary>
    public bool DrawConvexHull { get; set; } = false;

    /// <summary>
    /// 凸包颜色
    /// </summary>
    public Scalar ConvexHullColor { get; set; } = new Scalar(255, 255, 0);

    /// <summary>
    /// 凸包线条粗细
    /// </summary>
    public int ConvexHullThickness { get; set; } = 1;
}
