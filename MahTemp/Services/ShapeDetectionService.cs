using MahTemp.Enums;
using OpenCvSharp;

namespace MahTemp.Services;

/// <summary>
/// 基于 OpenCV 传统图像处理的形状检测服务
/// </summary>
public class ShapeDetectionService
{
    /// <summary>
    /// 检测结果
    /// </summary>
    public class ShapeResult
    {
        public DetectionLabel Label { get; set; }
        public string LabelName => Label.ToString();
        public Rect BoundingBox { get; set; }
        public Point[] Contour { get; set; } = [];
        public double Area { get; set; }
        public double Circularity { get; set; }
        public double AspectRatio { get; set; }
        public bool HasHole { get; set; }
        public int HoleCount { get; set; }
    }

    /// <summary>
    /// 检测图像中的金属形状
    /// </summary>
    public List<ShapeResult> Detect(Mat image, int minArea = 1000, int maxArea = int.MaxValue)
    {
        var results = new List<ShapeResult>();

        // 转灰度
        using var gray = new Mat();
        if (image.Channels() == 3)
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        else
            image.CopyTo(gray);

        // 强模糊去除木纹等纹理
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(15, 15), 0);

        // 使用 Canny 边缘检测
        using var edges = new Mat();
        Cv2.Canny(blurred, edges, 30, 100);

        // 膨胀边缘使轮廓闭合
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(7, 7));
        using var dilated = new Mat();
        Cv2.Dilate(edges, dilated, kernel, iterations: 2);

        // 闭运算填充内部
        using var closed = new Mat();
        Cv2.MorphologyEx(dilated, closed, MorphTypes.Close, kernel, iterations: 3);

        // 查找轮廓（带层级信息）
        Cv2.FindContours(closed, out Point[][] contours, out HierarchyIndex[] hierarchy,
            RetrievalModes.CComp, ContourApproximationModes.ApproxSimple);

        // 图像总面积，用于过滤太大的轮廓（如背景）
        double imageArea = image.Rows * image.Cols;
        double maxAllowedArea = Math.Min(maxArea, imageArea * 0.3); // 最大不超过图像30%

        // 分析每个轮廓
        for (int i = 0; i < contours.Length; i++)
        {
            var contour = contours[i];
            double area = Cv2.ContourArea(contour);

            // 过滤太小或太大的轮廓
            if (area < minArea || area > maxAllowedArea)
                continue;

            // 只处理外轮廓
            if (hierarchy[i].Parent != -1)
                continue;

            // 计算轮廓特征
            var boundingRect = Cv2.BoundingRect(contour);
            
            // 过滤贴边的轮廓（可能是背景边缘）
            if (boundingRect.X <= 5 || boundingRect.Y <= 5 ||
                boundingRect.Right >= image.Cols - 5 || boundingRect.Bottom >= image.Rows - 5)
                continue;

            double perimeter = Cv2.ArcLength(contour, true);
            double circularity = 4 * Math.PI * area / (perimeter * perimeter);
            
            // 使用旋转矩形获取更准确的长宽比
            var rotatedRect = Cv2.MinAreaRect(contour);
            double width = Math.Max(rotatedRect.Size.Width, rotatedRect.Size.Height);
            double height = Math.Min(rotatedRect.Size.Width, rotatedRect.Size.Height);
            double aspectRatio = height > 0 ? width / height : 1;

            // 检查是否有孔（子轮廓）
            int holeCount = CountHoles(hierarchy, i, contours, minArea / 10);
            bool hasHole = holeCount > 0;

            // 凸包分析
            var hull = Cv2.ConvexHull(contour);
            double hullArea = Cv2.ContourArea(hull);
            double solidity = hullArea > 0 ? area / hullArea : 0;

            // 矩形度：轮廓面积与边界矩形面积的比值
            double rectArea = boundingRect.Width * boundingRect.Height;
            double rectangularity = rectArea > 0 ? area / rectArea : 0;

            // 分类形状
            var label = ClassifyShape(circularity, aspectRatio, solidity, hasHole, rectangularity, contour);

            results.Add(new ShapeResult
            {
                Label = label,
                BoundingBox = boundingRect,
                Contour = contour,
                Area = area,
                Circularity = circularity,
                AspectRatio = aspectRatio,
                HasHole = hasHole,
                HoleCount = holeCount
            });
        }

        return results;
    }

    /// <summary>
    /// 在图像上绘制检测结果
    /// </summary>
    public Mat DrawResults(Mat image, List<ShapeResult> results)
    {
        var output = image.Clone();

        foreach (var result in results)
        {
            // 绘制边界框
            Cv2.Rectangle(output, result.BoundingBox, new Scalar(0, 0, 0), 2);

            // 绘制标签
            string label = $"{result.LabelName}";
            int textY = result.BoundingBox.Y - 5;
            if (textY < 15) textY = result.BoundingBox.Y + result.BoundingBox.Height + 15;

            Cv2.PutText(output, label, new Point(result.BoundingBox.X, textY),
                HersheyFonts.HersheySimplex, 0.5, new Scalar(0, 0, 0), 1);
        }

        return output;
    }

    private static int CountHoles(HierarchyIndex[] hierarchy, int contourIndex, Point[][] contours, double minHoleArea)
    {
        int count = 0;
        int childIndex = hierarchy[contourIndex].Child;

        while (childIndex != -1)
        {
            // 只计算足够大的孔
            double childArea = Cv2.ContourArea(contours[childIndex]);
            if (childArea >= minHoleArea)
                count++;
            childIndex = hierarchy[childIndex].Next;
        }

        return count;
    }

    private static DetectionLabel ClassifyShape(
        double circularity,
        double aspectRatio,
        double solidity,
        bool hasHole,
        double rectangularity,
        Point[] contour)
    {
        // 有孔的形状
        if (hasHole)
        {
            return DetectionLabel.EllipseWithHole;
        }

        // 钥匙形：有明显的"柄"，实心度较低，长宽比大
        // 钥匙形状一端是圆形，一端是细长的柄
        if (solidity < 0.75 && aspectRatio > 1.8)
        {
            return DetectionLabel.Key;
        }

        // 椭圆形：高实心度，高矩形度，长宽比适中到大
        if (solidity > 0.85 && rectangularity > 0.6 && aspectRatio > 1.5)
        {
            return DetectionLabel.Ellipse;
        }

        // 水滴形：一端尖一端圆，实心度中等偏高
        if (solidity > 0.75 && solidity < 0.92 && aspectRatio > 1.3)
        {
            return DetectionLabel.WaterDrop;
        }

        // 根据长宽比进一步判断
        if (aspectRatio > 2.0)
        {
            // 长条形更可能是椭圆
            return DetectionLabel.Ellipse;
        }
        else if (aspectRatio > 1.3)
        {
            // 中等长宽比，检查是否水滴形
            if (IsWaterDropShape(contour))
                return DetectionLabel.WaterDrop;
            return DetectionLabel.Ellipse;
        }

        // 默认水滴
        return DetectionLabel.WaterDrop;
    }

    private static bool IsWaterDropShape(Point[] contour)
    {
        if (contour.Length < 10) return false;

        var boundingRect = Cv2.BoundingRect(contour);
        int midY = boundingRect.Y + boundingRect.Height / 2;

        var topPoints = contour.Where(p => p.Y < midY).ToArray();
        var bottomPoints = contour.Where(p => p.Y >= midY).ToArray();

        if (topPoints.Length < 3 || bottomPoints.Length < 3) return false;

        double topWidth = topPoints.Max(p => p.X) - topPoints.Min(p => p.X);
        double bottomWidth = bottomPoints.Max(p => p.X) - bottomPoints.Min(p => p.X);

        // 水滴形：一端宽一端窄
        double widthRatio = Math.Max(topWidth, bottomWidth) / Math.Max(1, Math.Min(topWidth, bottomWidth));

        return widthRatio > 1.5;
    }
}
