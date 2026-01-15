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
        var (inputTensor, ratio, padW, padH) = PreprocessImage(image, 640, 640);

        // 执行推理
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(session.InputNames[0], inputTensor)
        };

        using var outputs = session.Run(inputs);
        var outputTensor = outputs.ElementAt(0).AsTensor<float>();
        var dims = outputTensor.Dimensions.ToArray();

        // 后处理 - YOLOv8/YOLO11 输出格式: [1, 4+num_classes, num_predictions]
        var results = PostProcess(outputTensor, dims, ratio, padW, padH, options, labels);

        // 过滤标签
        if (options.FilterLabels is { Length: > 0 })
        {
            results = results.Where(r => options.FilterLabels.Contains(r.LabelName)).ToList();
        }

        return results;
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

    private static (DenseTensor<float> tensor, float ratio, int padW, int padH) PreprocessImage(
        Mat image, int targetWidth, int targetHeight)
    {
        // 计算缩放比例，保持宽高比
        float ratio = Math.Min((float)targetWidth / image.Width, (float)targetHeight / image.Height);

        int newWidth = (int)(image.Width * ratio);
        int newHeight = (int)(image.Height * ratio);


        // 调整图像大小
        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(newWidth, newHeight));

        // 创建带填充的图像 (letterbox)
        int padW = (targetWidth - newWidth) / 2;
        int padH = (targetHeight - newHeight) / 2;
        
        using var padded = new Mat();
        Cv2.CopyMakeBorder(resized, padded, padH, targetHeight - newHeight - padH, 
            padW, targetWidth - newWidth - padW, BorderTypes.Constant, new Scalar(114, 114, 114));

        System.Diagnostics.Debug.WriteLine($"Padded size: {padded.Width}x{padded.Height}, Pad: ({padW}, {padH})");

        // 转换为 RGB
        using var rgb = new Mat();
        Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);

        // 转换为 float32 并归一化
        using var floatMat = new Mat();
        rgb.ConvertTo(floatMat, MatType.CV_32FC3, 1.0 / 255.0);

        // 创建张量 [1, 3, height, width] - NCHW 格式
        var tensor = new DenseTensor<float>([1, 3, targetHeight, targetWidth]);

        // 分离通道
        var channels = Cv2.Split(floatMat);
        
        for (int c = 0; c < 3; c++)
        {
            channels[c].GetArray(out float[] channelData);
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    tensor[0, c, y, x] = channelData[y * targetWidth + x];
                }
            }
            channels[c].Dispose();
        }

        return (tensor, ratio, padW, padH);
    }

    private static List<DetectionResult> PostProcess(
        Tensor<float> outputTensor,
        int[] dims,
        float ratio,
        int padW,
        int padH,
        DetectionOptions options,
        string[] labels)
    {
        var detections = new List<DetectionResult>();
        
        int numFeatures = dims[1];  // 4 + num_classes
        int numPredictions = dims[2];  // 8400
        int numClasses = numFeatures - 4;

        for (int i = 0; i < numPredictions; i++)
        {
            // 获取边界框坐标 (cx, cy, w, h) - 按列读取
            float cx = outputTensor[0, 0, i];
            float cy = outputTensor[0, 1, i];
            float bw = outputTensor[0, 2, i];
            float bh = outputTensor[0, 3, i];

            // 获取类别概率
            float maxScore = 0;
            int maxIndex = 0;
            for (int c = 0; c < numClasses; c++)
            {
                float score = outputTensor[0, 4 + c, i];
                if (score > maxScore)
                {
                    maxScore = score;
                    maxIndex = c;
                }
            }

            // 过滤低置信度检测
            if (maxScore < options.ConfidenceThreshold)
                continue;

            // 转换坐标到原图 (去除 padding 和缩放)
            float x1 = (cx - bw / 2 - padW) / ratio;
            float y1 = (cy - bh / 2 - padH) / ratio;
            float x2 = (cx + bw / 2 - padW) / ratio;
            float y2 = (cy + bh / 2 - padH) / ratio;

            // 确保坐标有效
            if (x2 <= x1 || y2 <= y1)
                continue;

            detections.Add(new DetectionResult
            {
                LabelIndex = maxIndex,
                LabelName = maxIndex < labels.Length ? labels[maxIndex] : $"class_{maxIndex}",
                Confidence = maxScore,
                BoundingBox = new Rect(
                    (int)Math.Max(0, x1),
                    (int)Math.Max(0, y1),
                    (int)(x2 - x1),
                    (int)(y2 - y1))
            });
        }


        // 应用 NMS
        return ApplyNMS(detections, options.NmsThreshold);
    }

    private static List<DetectionResult> ApplyNMS(List<DetectionResult> detections, float nmsThreshold)
    {
        List<DetectionResult> result = [];
        var sorted = detections.OrderByDescending(d => d.Confidence).ToList();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);

            sorted = [.. sorted.Where(d =>
            {
                if (d.LabelIndex != best.LabelIndex)
                    return true;

                float iou = CalculateIoU(best.BoundingBox, d.BoundingBox);
                return iou < nmsThreshold;
            })];
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
                label += $" {detection.Confidence:P1}";

            if (!string.IsNullOrEmpty(label))
            {
                // 计算文本大小
                var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, options.FontScale, 1, out int baseline);
                
                // 文本位置（在框上方）
                int textY = box.Y - baseline - 5;
                if (textY < textSize.Height) textY = box.Y + textSize.Height + 5;

                // 绘制文本
                var textColor = new Scalar(options.TextColor.B, options.TextColor.G, options.TextColor.R);
                Cv2.PutText(image, label, new Point(box.X, textY),
                    HersheyFonts.HersheySimplex, options.FontScale, textColor, 1);
            }
        }

        return image;
    }

    #endregion
}
