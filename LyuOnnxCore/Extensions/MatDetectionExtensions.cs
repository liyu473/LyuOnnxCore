using LyuOnnxCore.Models;
using LyuOnnxCore.Helpers;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// 基于 OpenCV Mat 的 ONNX 目标检测扩展方法
/// </summary>
public static class MatDetectionExtensions
{
    /// <summary>
    /// 执行目标检测
    /// </summary>
    /// <param name="session">ONNX 推理会话</param>
    /// <param name="image">OpenCV Mat 图像</param>
    /// <param name="labels">标签名称数组</param>
    /// <param name="options">检测选项</param>
    /// <returns>检测结果列表</returns>
    public static List<DetectionResult> Detect(
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

        // 获取模型输入尺寸
        var (inputWidth, inputHeight) = GetModelInputSize(session, options);

        // 预处理图像
        var (inputTensor, ratio, padW, padH) = PreprocessImage(image, inputWidth, inputHeight);

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
        var results = PostProcess(outputTensor, dims, ratio, padW, padH, options, labels, image.Width, image.Height);

        // 过滤标签
        if (options.FilterLabels is { Length: > 0 })
        {
            results = [.. results.Where(r => options.FilterLabels.Contains(r.LabelName))];
        }

        // 过滤重叠框
        if (options.IsFilterOverlay)
        {
            results = results.FilterContained(options.OverlayThreshold, options.IsCrossClass);
        }

        return results;
    }

    /// <summary>
    /// 在 Mat 图像上绘制检测结果
    /// </summary>
    /// <param name="image">原始图像</param>
    /// <param name="detections">检测结果列表</param>
    /// <param name="options">绘制选项</param>
    /// <returns>绘制后的图像（新的 Mat 对象）</returns>
    public static Mat DrawDetections(
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
            if (!detection.BoundingBox.HasValue)
                continue;

            var box = detection.BoundingBox.Value;
            var rect = new Rect(box.X, box.Y, box.Width, box.Height);
            var color = new Scalar(options.BoxColor.B, options.BoxColor.G, options.BoxColor.R);

            // 绘制边界框
            Cv2.Rectangle(result, rect, color, options.BoxThickness);

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
                        new Point(box.X, box.Y),
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
                    
                    // 绘制文本背景
                    var textRect = new Rect(box.X, box.Y - textSize.Height - baseline - 5, textSize.Width + 5, textSize.Height + baseline + 5);
                    Cv2.Rectangle(result, textRect, color, -1);

                    // 绘制文本
                    Cv2.PutText(result, label, new Point(box.X + 2, box.Y - baseline - 2), 
                        HersheyFonts.HersheySimplex, options.FontScale, textColor, 1, LineTypes.AntiAlias);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 执行检测并绘制结果
    /// </summary>
    /// <param name="session">ONNX 推理会话</param>
    /// <param name="image">OpenCV Mat 图像</param>
    /// <param name="labels">标签名称数组</param>
    /// <param name="detectionOptions">检测选项</param>
    /// <param name="drawOptions">绘制选项</param>
    /// <returns>绘制后的图像</returns>
    public static Mat DetectAndDraw(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? detectionOptions = null,
        DrawOptions? drawOptions = null)
    {
        var results = session.Detect(image, labels, detectionOptions);
        return image.DrawDetections(results, drawOptions);
    }

    #region 私有辅助方法

    /// <summary>
    /// 获取模型输入尺寸
    /// </summary>
    private static (int width, int height) GetModelInputSize(InferenceSession session, DetectionOptions options)
    {
        // 如果用户指定了尺寸，使用用户指定的
        if (options.InputWidth.HasValue && options.InputHeight.HasValue)
        {
            return (options.InputWidth.Value, options.InputHeight.Value);
        }

        // 尝试从模型元数据获取
        try
        {
            var inputMetadata = session.InputMetadata[session.InputNames[0]];
            var shape = inputMetadata.Dimensions;
            
            // YOLO 模型输入格式通常是 [batch, channels, height, width]
            if (shape.Length == 4)
            {
                int height = shape[2];
                int width = shape[3];
                
                // 如果是动态尺寸（-1），使用默认值
                if (height > 0 && width > 0)
                {
                    return (width, height);
                }
            }
        }
        catch
        {
            // 如果获取失败，使用默认值
        }

        // 默认使用 640x640
        return (640, 640);
    }

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
    /// <summary>
    /// Sigmoid 激活函数
    /// </summary>
    private static float Sigmoid(float x)
    {
        return 1.0f / (1.0f + MathF.Exp(-x));
    }

    /// <summary>
    /// 后处理：解析模型输出，应用 NMS
    /// </summary>
    private static List<DetectionResult> PostProcess(
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
        int numClasses = numFeatures - 4;

        for (int i = 0; i < numPredictions; i++)
        {
            // 获取边界框坐标（中心点格式）
            float cx = outputTensor[0, 0, i];
            float cy = outputTensor[0, 1, i];
            float bw = outputTensor[0, 2, i];
            float bh = outputTensor[0, 3, i];

            // 找到最高置信度的类别
            // ⚠️ 关键: ONNX 输出没有经过 Sigmoid，需要手动应用
            float maxScore = 0;
            int maxIndex = 0;
            for (int c = 0; c < numClasses; c++)
            {
                float rawScore = outputTensor[0, 4 + c, i];
                float score = Sigmoid(rawScore);  // 应用 Sigmoid 激活
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
            float x1 = (cx - bw / 2 - padW) / ratio;
            float y1 = (cy - bh / 2 - padH) / ratio;
            float x2 = (cx + bw / 2 - padW) / ratio;
            float y2 = (cy + bh / 2 - padH) / ratio;

            // 确保坐标有效
            if (x2 <= x1 || y2 <= y1)
                continue;

            // 裁剪到图像边界
            x1 = Math.Max(0, x1);
            y1 = Math.Max(0, y1);
            x2 = Math.Min(originalWidth, x2);
            y2 = Math.Min(originalHeight, y2);

            detections.Add(new DetectionResult
            {
                LabelIndex = maxIndex,
                LabelName = maxIndex < labels.Length ? labels[maxIndex] : $"class_{maxIndex}",
                Confidence = maxScore,
                BoundingBox = new BoundingBox(
                    (int)x1,
                    (int)y1,
                    (int)(x2 - x1),
                    (int)(y2 - y1))
            });
        }

        // 应用 NMS
        return ApplyNMS(detections, options.NmsThreshold);
    }

    /// <summary>
    /// 非极大值抑制（NMS）
    /// </summary>
    private static List<DetectionResult> ApplyNMS(List<DetectionResult> detections, float nmsThreshold)
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

                // 只处理标准边界框
                if (!best.BoundingBox.HasValue || !d.BoundingBox.HasValue)
                    return true;

                float iou = CalculateIoU(best.BoundingBox.Value, d.BoundingBox.Value);
                return iou < nmsThreshold;
            })];
        }

        return result;
    }

    /// <summary>
    /// 计算两个边界框的 IoU
    /// </summary>
    private static float CalculateIoU(BoundingBox box1, BoundingBox box2)
    {
        int x1 = Math.Max(box1.X, box2.X);
        int y1 = Math.Max(box1.Y, box2.Y);
        int x2 = Math.Min(box1.Right, box2.Right);
        int y2 = Math.Min(box1.Bottom, box2.Bottom);

        int intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        int union = box1.Area + box2.Area - intersection;

        return union > 0 ? (float)intersection / union : 0;
    }

    #endregion
}
