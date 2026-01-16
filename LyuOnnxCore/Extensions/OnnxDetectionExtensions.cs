using LyuOnnxCore.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// ONNX 目标检测扩展方法（与图像库解耦）
/// </summary>
public static class OnnxDetectionExtensions
{
    /// <summary>
    /// 执行目标检测
    /// </summary>
    /// <param name="session">ONNX 推理会话</param>
    /// <param name="imageData">图像数据</param>
    /// <param name="labels">标签名称数组</param>
    /// <param name="options">检测选项</param>
    /// <returns>检测结果列表</returns>
    public static List<DetectionResult> Detect(
        this InferenceSession session,
        ImageData imageData,
        string[] labels,
        DetectionOptions? options = null)
    {
        if (labels == null || labels.Length == 0)
            throw new ArgumentException("标签数组不能为空", nameof(labels));

        options ??= new DetectionOptions();

        var (inputTensor, ratio, padW, padH) = PreprocessImage(imageData, 640, 640);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(session.InputNames[0], inputTensor)
        };

        using var outputs = session.Run(inputs);
        var outputTensor = outputs.ElementAt(0).AsTensor<float>();
        var dims = outputTensor.Dimensions.ToArray();

        var results = PostProcess(outputTensor, dims, ratio, padW, padH, options, labels);

        if (options.FilterLabels is { Length: > 0 })
        {
            results = results.Where(r => options.FilterLabels.Contains(r.LabelName)).ToList();
        }

        return results;
    }

    #region 私有辅助方法

    private static (DenseTensor<float> tensor, float ratio, int padW, int padH) PreprocessImage(
        ImageData image, int targetWidth, int targetHeight)
    {
        float ratio = Math.Min((float)targetWidth / image.Width, (float)targetHeight / image.Height);

        int newWidth = (int)(image.Width * ratio);
        int newHeight = (int)(image.Height * ratio);

        int padW = (targetWidth - newWidth) / 2;
        int padH = (targetHeight - newHeight) / 2;

        var tensor = new DenseTensor<float>([1, 3, targetHeight, targetWidth]);

        // 填充灰色背景 (114/255)
        float grayValue = 114f / 255f;
        for (int c = 0; c < 3; c++)
            for (int y = 0; y < targetHeight; y++)
                for (int x = 0; x < targetWidth; x++)
                    tensor[0, c, y, x] = grayValue;

        // 填充图像数据
        for (int y = 0; y < newHeight; y++)
        {
            int srcY = (int)(y / ratio);
            if (srcY >= image.Height) srcY = image.Height - 1;

            for (int x = 0; x < newWidth; x++)
            {
                int srcX = (int)(x / ratio);
                if (srcX >= image.Width) srcX = image.Width - 1;

                int srcIdx = (srcY * image.Width + srcX) * image.Channels;
                int dstY = y + padH;
                int dstX = x + padW;

                if (image.IsBgr)
                {
                    tensor[0, 0, dstY, dstX] = image.Data[srcIdx + 2] / 255f; // R
                    tensor[0, 1, dstY, dstX] = image.Data[srcIdx + 1] / 255f; // G
                    tensor[0, 2, dstY, dstX] = image.Data[srcIdx + 0] / 255f; // B
                }
                else
                {
                    tensor[0, 0, dstY, dstX] = image.Data[srcIdx + 0] / 255f; // R
                    tensor[0, 1, dstY, dstX] = image.Data[srcIdx + 1] / 255f; // G
                    tensor[0, 2, dstY, dstX] = image.Data[srcIdx + 2] / 255f; // B
                }
            }
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

        int numFeatures = dims[1];
        int numPredictions = dims[2];
        int numClasses = numFeatures - 4;

        for (int i = 0; i < numPredictions; i++)
        {
            float cx = outputTensor[0, 0, i];
            float cy = outputTensor[0, 1, i];
            float bw = outputTensor[0, 2, i];
            float bh = outputTensor[0, 3, i];

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

            if (maxScore < options.ConfidenceThreshold)
                continue;

            float x1 = (cx - bw / 2 - padW) / ratio;
            float y1 = (cy - bh / 2 - padH) / ratio;
            float x2 = (cx + bw / 2 - padW) / ratio;
            float y2 = (cy + bh / 2 - padH) / ratio;

            if (x2 <= x1 || y2 <= y1)
                continue;

            detections.Add(new DetectionResult
            {
                LabelIndex = maxIndex,
                LabelName = maxIndex < labels.Length ? labels[maxIndex] : $"class_{maxIndex}",
                Confidence = maxScore,
                BoundingBox = new BoundingBox(
                    (int)Math.Max(0, x1),
                    (int)Math.Max(0, y1),
                    (int)(x2 - x1),
                    (int)(y2 - y1))
            });
        }

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
