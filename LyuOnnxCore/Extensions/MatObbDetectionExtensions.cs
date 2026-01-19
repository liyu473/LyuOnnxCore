using LyuOnnxCore.Models;
using LyuOnnxCore.Helpers;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// 基于 OpenCV Mat 的 ONNX OBB (Oriented Bounding Box) 目标检测扩展方法
/// </summary>
public static class MatObbDetectionExtensions
{
    /// <summary>
    /// 执行 OBB 目标检测
    /// </summary>
    /// <param name="session">ONNX 推理会话</param>
    /// <param name="image">OpenCV Mat 图像</param>
    /// <param name="labels">标签名称数组</param>
    /// <param name="options">检测选项</param>
    /// <returns>检测结果列表</returns>
    public static List<DetectionResult> DetectOBB(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? options = null)
    {
        if (image == null || image.Empty())
            throw new ArgumentException("图像不能为空", nameof(image));

        if (labels == null || labels.Length == 0)
            throw new ArgumentException("标签数组不能为空", nameof(labels));

        options ??= new DetectionOptions();

        // 预处理图像
        var (inputTensor, ratio, padW, padH) = PreprocessImage(image, 640, 640);

        // 创建输入
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(session.InputNames[0], inputTensor)
        };

        // 执行推理
        using var outputs = session.Run(inputs);
        var outputTensor = outputs.ElementAt(0).AsTensor<float>();
        var dims = outputTensor.Dimensions.ToArray();

        // 后处理
        var results = PostProcessOBB(outputTensor, dims, ratio, padW, padH, options, labels, image.Width, image.Height);

        // 过滤标签
        if (options.FilterLabels is { Length: > 0 })
        {
            results = [.. results.Where(r => options.FilterLabels.Contains(r.LabelName))];
        }

        // 过滤重叠框
        if (options.IsFilterOverlay)
        {
            results = results.FilterContainedOBB(options.OverlayThreshold, options.IsCrossClass);
        }

        return results;
    }

    /// <summary>
    /// 在 Mat 图像上绘制 OBB 检测结果
    /// </summary>
    /// <param name="image">原始图像</param>
    /// <param name="detections">检测结果列表</param>
    /// <param name="options">绘制选项</param>
    /// <returns>绘制后的图像（新的 Mat 对象）</returns>
    public static Mat DrawOBBDetections(
        this Mat image,
        List<DetectionResult> detections,
        DrawOptions? options = null)
    {
        if (image == null || image.Empty())
            throw new ArgumentException("图像不能为空", nameof(image));

        options ??= new DrawOptions();

        // 克隆图像以避免修改原图
        var result = image.Clone();

        foreach (var detection in detections)
        {
            if (!detection.IsOBB || !detection.OrientedBoundingBox.HasValue)
                continue;

            var obb = detection.OrientedBoundingBox.Value;
            var corners = obb.GetCornerPoints();
            var color = new Scalar(options.BoxColor.B, options.BoxColor.G, options.BoxColor.R);

            // 绘制旋转边界框的四条边
            for (int i = 0; i < 4; i++)
            {
                var pt1 = new Point((int)corners[i].X, (int)corners[i].Y);
                var pt2 = new Point((int)corners[(i + 1) % 4].X, (int)corners[(i + 1) % 4].Y);
                Cv2.Line(result, pt1, pt2, color, options.BoxThickness, LineTypes.AntiAlias);
            }

            // 准备标签文本
            string label = "";
            if (options.ShowLabel && options.ShowConfidence)
            {
                label = $"{detection.LabelName} {detection.Confidence:P0}";
            }
            else if (options.ShowLabel)
            {
                label = detection.LabelName;
            }
            else if (options.ShowConfidence)
            {
                label = $"{detection.Confidence:P0}";
            }

            // 绘制标签
            if (!string.IsNullOrEmpty(label))
            {
                if (options.UseChineseFont)
                {
                    // 使用中文字体绘制
                    var textColor = new Scalar(options.TextColor.B, options.TextColor.G, options.TextColor.R);
                    ChineseTextHelper.PutChineseText(
                        result,
                        label,
                        new Point((int)corners[0].X, (int)corners[0].Y),
                        options.ChineseFontFamily,
                        options.ChineseFontSize,
                        textColor,
                        color,
                        options.BoxThickness);
                }
                else
                {
                    // 使用英文字体绘制
                    var textColor = new Scalar(options.TextColor.B, options.TextColor.G, options.TextColor.R);
                    var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, options.FontScale, 1, out int baseline);

                    // 在第一个角点位置绘制文本背景
                    var textPos = new Point((int)corners[0].X, (int)corners[0].Y - 5);
                    var textRect = new Rect(textPos.X, textPos.Y - textSize.Height - baseline, textSize.Width + 5, textSize.Height + baseline + 5);
                    Cv2.Rectangle(result, textRect, color, -1);

                    // 绘制文本
                    Cv2.PutText(result, label, new Point(textPos.X + 2, textPos.Y - baseline),
                        HersheyFonts.HersheySimplex, options.FontScale, textColor, 1, LineTypes.AntiAlias);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 执行 OBB 检测并绘制结果
    /// </summary>
    /// <param name="session">ONNX 推理会话</param>
    /// <param name="image">OpenCV Mat 图像</param>
    /// <param name="labels">标签名称数组</param>
    /// <param name="detectionOptions">检测选项</param>
    /// <param name="drawOptions">绘制选项</param>
    /// <returns>绘制后的图像</returns>
    public static Mat DetectOBBAndDraw(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? detectionOptions = null,
        DrawOptions? drawOptions = null)
    {
        var results = session.DetectOBB(image, labels, detectionOptions);
        return image.DrawOBBDetections(results, drawOptions);
    }

    #region 私有辅助方法

    /// <summary>
    /// 预处理图像：调整大小、填充、归一化
    /// </summary>
    private static (DenseTensor<float> tensor, float ratio, int padW, int padH) PreprocessImage(
        Mat image, int targetWidth, int targetHeight)
    {
        // 计算缩放比例
        float ratio = Math.Min((float)targetWidth / image.Width, (float)targetHeight / image.Height);

        int newWidth = (int)(image.Width * ratio);
        int newHeight = (int)(image.Height * ratio);

        // 计算填充
        int padW = (targetWidth - newWidth) / 2;
        int padH = (targetHeight - newHeight) / 2;

        // 调整图像大小
        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(newWidth, newHeight), interpolation: InterpolationFlags.Linear);

        // 创建填充后的图像（灰色背景 114）
        using var padded = new Mat(targetHeight, targetWidth, MatType.CV_8UC3, new Scalar(114, 114, 114));

        // 将调整大小后的图像复制到中心
        var roi = new Rect(padW, padH, newWidth, newHeight);
        resized.CopyTo(new Mat(padded, roi));

        // 转换为 RGB（OpenCV 默认是 BGR）
        using var rgb = new Mat();
        Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);

        // 创建张量并归一化
        var tensor = new DenseTensor<float>([1, 3, targetHeight, targetWidth]);

        // 填充张量数据 (HWC -> CHW 并归一化到 0-1)
        unsafe
        {
            byte* ptr = (byte*)rgb.DataPointer;
            int channels = rgb.Channels();

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int idx = (y * targetWidth + x) * channels;
                    tensor[0, 0, y, x] = ptr[idx + 0] / 255f; // R
                    tensor[0, 1, y, x] = ptr[idx + 1] / 255f; // G
                    tensor[0, 2, y, x] = ptr[idx + 2] / 255f; // B
                }
            }
        }

        return (tensor, ratio, padW, padH);
    }

    /// <summary>
    /// 后处理：解析 OBB 模型输出，应用 NMS
    /// OBB 模型输出格式: [batch, 5+num_classes, num_predictions]
    /// 其中 5 个参数为: cx, cy, w, h, angle
    /// </summary>
    private static List<DetectionResult> PostProcessOBB(
        Tensor<float> outputTensor,
        int[] dims,
        float ratio,
        int padW,
        int padH,
        DetectionOptions options,
        string[] labels,
        int originalWidth,
        int originalHeight)
    {
        var detections = new List<DetectionResult>();

        int numFeatures = dims[1];
        int numPredictions = dims[2];
        int numClasses = numFeatures - 5; // OBB 模型有 5 个额外参数

        for (int i = 0; i < numPredictions; i++)
        {
            // 获取边界框坐标和角度（中心点格式）
            float cx = outputTensor[0, 0, i];
            float cy = outputTensor[0, 1, i];
            float bw = outputTensor[0, 2, i];
            float bh = outputTensor[0, 3, i];
            float angle = outputTensor[0, 4, i]; // 旋转角度（弧度）

            // 找到最高置信度的类别
            float maxScore = 0;
            int maxIndex = 0;
            for (int c = 0; c < numClasses; c++)
            {
                float score = outputTensor[0, 5 + c, i];
                if (score > maxScore)
                {
                    maxScore = score;
                    maxIndex = c;
                }
            }

            // 过滤低置信度
            if (maxScore < options.ConfidenceThreshold)
                continue;

            // 转换为原始图像坐标
            float centerX = (cx - padW) / ratio;
            float centerY = (cy - padH) / ratio;
            float width = bw / ratio;
            float height = bh / ratio;

            // 确保坐标有效
            if (width <= 0 || height <= 0)
                continue;

            // 裁剪到图像边界
            centerX = Math.Clamp(centerX, 0, originalWidth);
            centerY = Math.Clamp(centerY, 0, originalHeight);

            detections.Add(new DetectionResult
            {
                LabelIndex = maxIndex,
                LabelName = maxIndex < labels.Length ? labels[maxIndex] : $"class_{maxIndex}",
                Confidence = maxScore,
                OrientedBoundingBox = new OrientedBoundingBox(centerX, centerY, width, height, angle)
            });
        }

        // 应用 NMS
        return ApplyNMSOBB(detections, options.NmsThreshold);
    }

    /// <summary>
    /// 非极大值抑制（NMS）for OBB
    /// </summary>
    private static List<DetectionResult> ApplyNMSOBB(List<DetectionResult> detections, float nmsThreshold)
    {
        var result = new List<DetectionResult>();
        var sorted = detections.OrderByDescending(d => d.Confidence).ToList();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);

            sorted = [.. sorted.Where(d =>
            {
                // 不同类别不进行 NMS
                if (d.LabelIndex != best.LabelIndex)
                    return true;

                // 计算旋转框的 IoU
                float iou = CalculateOBBIoU(best.OrientedBoundingBox!.Value, d.OrientedBoundingBox!.Value);
                return iou < nmsThreshold;
            })];
        }

        return result;
    }

    /// <summary>
    /// 计算两个旋转边界框的 IoU（使用 OpenCV 的旋转矩形交集）
    /// </summary>
    private static float CalculateOBBIoU(OrientedBoundingBox obb1, OrientedBoundingBox obb2)
    {
        // 创建 OpenCV 的 RotatedRect
        var rect1 = new RotatedRect(
            new Point2f(obb1.CenterX, obb1.CenterY),
            new Size2f(obb1.Width, obb1.Height),
            (float)(obb1.Angle * 180 / Math.PI) // 转换为角度
        );

        var rect2 = new RotatedRect(
            new Point2f(obb2.CenterX, obb2.CenterY),
            new Size2f(obb2.Width, obb2.Height),
            (float)(obb2.Angle * 180 / Math.PI)
        );

        // 使用 OpenCV 计算旋转矩形的交集
        using var intersectionPoints = new Mat();
        var intersectionType = Cv2.RotatedRectangleIntersection(rect1, rect2, intersectionPoints);

        float intersectionArea = 0;

        if (intersectionType == RectanglesIntersectTypes.None)
        {
            intersectionArea = 0;
        }
        else if (intersectionType == RectanglesIntersectTypes.Full)
        {
            // 完全重叠，取较小的面积
            intersectionArea = Math.Min(rect1.Size.Width * rect1.Size.Height, 
                                       rect2.Size.Width * rect2.Size.Height);
        }
        else
        {
            // 部分重叠，计算交集多边形的面积
            if (!intersectionPoints.Empty() && intersectionPoints.Rows >= 3)
            {
                var points = new Point2f[intersectionPoints.Rows];
                for (int i = 0; i < intersectionPoints.Rows; i++)
                {
                    points[i] = new Point2f(
                        intersectionPoints.At<float>(i, 0),
                        intersectionPoints.At<float>(i, 1)
                    );
                }
                intersectionArea = (float)Math.Abs(Cv2.ContourArea(points));
            }
        }

        float area1 = rect1.Size.Width * rect1.Size.Height;
        float area2 = rect2.Size.Width * rect2.Size.Height;
        float unionArea = area1 + area2 - intersectionArea;

        return unionArea > 0 ? intersectionArea / unionArea : 0;
    }

    #endregion
}
