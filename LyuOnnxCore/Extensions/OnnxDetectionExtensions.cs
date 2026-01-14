using LyuOnnxCore.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// ONNX 目标检测扩展方法
/// </summary>
public static class OnnxDetectionExtensions
{
    /// <summary>
    /// 执行目标检测并返回带标注的图像
    /// </summary>
    /// <param name="session">ONNX 推理会话</param>
    /// <param name="image">输入图像</param>
    /// <param name="labels">标签名称数组（必需）</param>
    /// <param name="options">检测选项（可选）</param>
    /// <returns>带标注的图像</returns>
    public static Mat DetectAndDrawBoxes(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? options = null)
    {
        if (labels == null || labels.Length == 0)
            throw new ArgumentException("标签数组不能为空", nameof(labels));

        options ??= new DetectionOptions();

        var results = session.Detect(image, labels, options);
        return DrawDetections(image.Clone(), results, options);
    }

    /// <summary>
    /// 执行目标检测并返回检测结果列表
    /// </summary>
    /// <param name="session">ONNX 推理会话</param>
    /// <param name="image">输入图像</param>
    /// <param name="labels">标签名称数组（必需）</param>
    /// <param name="options">检测选项（可选）</param>
    /// <returns>检测结果列表</returns>
    public static List<DetectionResult> Detect(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? options = null)
    {
        if (labels == null || labels.Length == 0)
            throw new ArgumentException("标签数组不能为空", nameof(labels));

        options ??= new DetectionOptions();

        // 预处理图像
        var (inputTensor, ratioW, ratioH, padW, padH) = PreprocessImage(image, 640, 640);

        // 执行推理
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(session.InputNames[0], inputTensor)
        };

        using var outputs = session.Run(inputs);
        var output = outputs.First().AsEnumerable<float>().ToArray();

        // 后处理
        return PostProcess(image, output, ratioW, ratioH, padW, padH, options, labels);
    }

    /// <summary>
    /// 获取裁剪后的检测区域列表
    /// </summary>
    /// <param name="session">ONNX 推理会话</param>
    /// <param name="image">输入图像</param>
    /// <param name="labels">标签名称数组（必需）</param>
    /// <param name="options">检测选项（可选）</param>
    /// <returns>检测结果列表，包含裁剪图像</returns>
    public static List<DetectionResult> DetectAndCrop(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? options = null)
    {
        var results = session.Detect(image, labels, options);
        
        foreach (var result in results)
        {
            // 确保边界框在图像范围内
            var rect = result.BoundingBox;
            rect.X = Math.Max(0, rect.X);
            rect.Y = Math.Max(0, rect.Y);
            rect.Width = Math.Min(image.Width - rect.X, rect.Width);
            rect.Height = Math.Min(image.Height - rect.Y, rect.Height);

            // 裁剪图像区域
            result.CroppedImage = new Mat(image, rect);
        }

        return results;
    }

    #region 私有辅助方法

    private static (DenseTensor<float> tensor, float ratioW, float ratioH, int padW, int padH) PreprocessImage(
        Mat image, int targetWidth, int targetHeight)
    {
        // 计算缩放比例，保持宽高比
        float ratioW = (float)targetWidth / image.Width;
        float ratioH = (float)targetHeight / image.Height;
        float ratio = Math.Min(ratioW, ratioH);

        int newWidth = (int)(image.Width * ratio);
        int newHeight = (int)(image.Height * ratio);

        // 调整图像大小
        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(newWidth, newHeight));

        // 创建带填充的图像
        using var padded = new Mat(targetHeight, targetWidth, MatType.CV_8UC3, new Scalar(114, 114, 114));
        int padW = (targetWidth - newWidth) / 2;
        int padH = (targetHeight - newHeight) / 2;
        
        resized.CopyTo(new Mat(padded, new Rect(padW, padH, newWidth, newHeight)));

        // 转换为 RGB 并归一化
        using var rgb = new Mat();
        Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);

        // 创建张量 [1, 3, height, width]
        var tensor = new DenseTensor<float>(new[] { 1, 3, targetHeight, targetWidth });
        
        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                var pixel = rgb.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = pixel.Item0 / 255f;
                tensor[0, 1, y, x] = pixel.Item1 / 255f;
                tensor[0, 2, y, x] = pixel.Item2 / 255f;
            }
        }

        return (tensor, ratio, ratio, padW, padH);
    }

    private static List<DetectionResult> PostProcess(
        Mat originalImage,
        float[] output,
        float ratioW,
        float ratioH,
        int padW,
        int padH,
        DetectionOptions options,
        string[] labels)
    {
        var detections = new List<DetectionResult>();
        
        // YOLOv8 输出格式: [1, 84, 8400] -> [batch, 4 + num_classes, num_predictions]
        int numClasses = labels.Length;
        int numPredictions = output.Length / (4 + numClasses);

        for (int i = 0; i < numPredictions; i++)
        {
            // 获取边界框坐标 (cx, cy, w, h)
            float cx = output[i];
            float cy = output[i + numPredictions];
            float w = output[i + numPredictions * 2];
            float h = output[i + numPredictions * 3];

            // 获取类别概率
            float maxScore = 0;
            int maxIndex = 0;
            for (int c = 0; c < numClasses; c++)
            {
                float score = output[i + numPredictions * (4 + c)];
                if (score > maxScore)
                {
                    maxScore = score;
                    maxIndex = c;
                }
            }

            // 过滤低置信度检测
            if (maxScore < options.ConfidenceThreshold)
                continue;

            // 转换坐标到原图
            float x1 = (cx - w / 2 - padW) / ratioW;
            float y1 = (cy - h / 2 - padH) / ratioH;
            float x2 = (cx + w / 2 - padW) / ratioW;
            float y2 = (cy + h / 2 - padH) / ratioH;

            detections.Add(new DetectionResult
            {
                LabelIndex = maxIndex,
                LabelName = labels[maxIndex],
                Confidence = maxScore,
                BoundingBox = new Rect(
                    (int)x1,
                    (int)y1,
                    (int)(x2 - x1),
                    (int)(y2 - y1))
            });
        }

        // 应用 NMS
        return ApplyNMS(detections, options.NmsThreshold);
    }

    private static List<DetectionResult> ApplyNMS(List<DetectionResult> detections, float nmsThreshold)
    {
        var result = new List<DetectionResult>();
        var sorted = detections.OrderByDescending(d => d.Confidence).ToList();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);

            sorted = sorted.Where(d =>
            {
                if (d.LabelIndex != best.LabelIndex)
                    return true;

                float iou = CalculateIoU(best.BoundingBox, d.BoundingBox);
                return iou < nmsThreshold;
            }).ToList();
        }

        return result;
    }

    private static float CalculateIoU(Rect box1, Rect box2)
    {
        int x1 = Math.Max(box1.X, box2.X);
        int y1 = Math.Max(box1.Y, box2.Y);
        int x2 = Math.Min(box1.X + box1.Width, box2.X + box2.Width);
        int y2 = Math.Min(box1.Y + box1.Height, box2.Y + box2.Height);

        int intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        int union = box1.Width * box1.Height + box2.Width * box2.Height - intersection;

        return union > 0 ? (float)intersection / union : 0;
    }

    private static Mat DrawDetections(Mat image, List<DetectionResult> detections, DetectionOptions options)
    {
        foreach (var detection in detections)
        {
            var box = detection.BoundingBox;
            var color = new Scalar(options.BoxColor.B, options.BoxColor.G, options.BoxColor.R);

            // 绘制边界框
            Cv2.Rectangle(image, box, color, options.BoxThickness);

            // 准备标签文本
            string label = "";
            if (options.ShowLabel)
                label = detection.LabelName;
            if (options.ShowConfidence)
                label += $" {detection.Confidence:P0}";

            if (!string.IsNullOrEmpty(label))
            {
                // 计算文本大小
                var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, options.FontScale, 1, out int baseline);
                
                // 绘制文本背景
                var textBg = new Rect(box.X, box.Y - textSize.Height - baseline - 5, textSize.Width + 10, textSize.Height + baseline + 5);
                var bgColor = new Scalar(options.TextBackgroundColor.B, options.TextBackgroundColor.G, options.TextBackgroundColor.R);
                Cv2.Rectangle(image, textBg, bgColor, -1);

                // 绘制文本
                var textColor = new Scalar(options.TextColor.B, options.TextColor.G, options.TextColor.R);
                Cv2.PutText(image, label, new Point(box.X + 5, box.Y - baseline - 5),
                    HersheyFonts.HersheySimplex, options.FontScale, textColor, 1);
            }
        }

        return image;
    }

    #endregion
}
