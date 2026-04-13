using LyuOnnxCore.Helpers;
using LyuOnnxCore.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// Horizontal bounding box detection helpers for OpenCV Mat.
/// </summary>
public static class MatDetectionExtensions
{
    public static List<HbbDetectionResult> Detect(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? options = null
    )
    {
        if (image is null || image.Empty())
        {
            throw new ArgumentException("Image cannot be null or empty.", nameof(image));
        }

        if (labels is null || labels.Length == 0)
        {
            throw new ArgumentException("Labels cannot be null or empty.", nameof(labels));
        }

        options ??= new DetectionOptions();

        var (inputWidth, inputHeight) = GetModelInputSize(session, options);
        var (inputTensor, ratio, padW, padH) = PreprocessImage(image, inputWidth, inputHeight);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(session.InputNames[0], inputTensor)
        };

        using var outputs = session.Run(inputs);
        var outputTensor = outputs.ElementAt(0).AsTensor<float>();
        var dims = outputTensor.Dimensions.ToArray();

        var results = PostProcess(outputTensor, dims, ratio, padW, padH, options, labels, image.Width, image.Height);

        if (options.FilterLabels is { Length: > 0 })
        {
            results = [.. results.Where(result => options.FilterLabels.Contains(result.LabelName))];
        }

        if (options.IsFilterOverlay)
        {
            results = results.FilterContained(options.OverlayThreshold, options.IsCrossClass);
        }

        return results;
    }

    public static Mat DrawDetections(
        this Mat image,
        IEnumerable<HbbDetectionResult> detections,
        DrawOptions? options = null
    )
    {
        if (image is null || image.Empty())
        {
            throw new ArgumentException("Image cannot be null or empty.", nameof(image));
        }

        options ??= new DrawOptions();
        var result = image.Clone();
        int index = 0;

        foreach (var detection in detections)
        {
            var box = detection.BoundingBox;
            var rect = new Rect(box.X, box.Y, box.Width, box.Height);
            var color = new Scalar(options.BoxColor.B, options.BoxColor.G, options.BoxColor.R);

            Cv2.Rectangle(result, rect, color, options.BoxThickness);

            string label = options switch
            {
                { ShowLabel: true, ShowConfidence: true } => $"[{index}] {detection.LabelName} {detection.Confidence:P0}",
                { ShowLabel: true } => $"[{index}] {detection.LabelName}",
                { ShowConfidence: true } => $"[{index}] {detection.Confidence:P0}",
                _ => $"[{index}]",
            };

            index++;
            if (string.IsNullOrEmpty(label))
            {
                continue;
            }

            var textColor = new Scalar(options.TextColor.B, options.TextColor.G, options.TextColor.R);
            if (options.UseChineseFont)
            {
                ChineseTextHelper.PutChineseText(
                    result,
                    label,
                    new Point(box.X, box.Y),
                    options.ChineseFontFamily,
                    options.ChineseFontSize,
                    textColor,
                    color,
                    options.BoxThickness
                );
            }
            else
            {
                var textSize = Cv2.GetTextSize(
                    label,
                    HersheyFonts.HersheySimplex,
                    options.FontScale,
                    1,
                    out int baseline
                );

                var textRect = new Rect(
                    box.X,
                    box.Y - textSize.Height - baseline - 5,
                    textSize.Width + 5,
                    textSize.Height + baseline + 5
                );
                Cv2.Rectangle(result, textRect, color, -1);
                Cv2.PutText(
                    result,
                    label,
                    new Point(box.X + 2, box.Y - baseline - 2),
                    HersheyFonts.HersheySimplex,
                    options.FontScale,
                    textColor,
                    1,
                    LineTypes.AntiAlias
                );
            }
        }

        return result;
    }

    public static Mat DetectAndDraw(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? detectionOptions = null,
        DrawOptions? drawOptions = null
    )
    {
        var results = session.Detect(image, labels, detectionOptions);
        return image.DrawDetections(results, drawOptions);
    }

    private static (int width, int height) GetModelInputSize(
        InferenceSession session,
        DetectionOptions options
    )
    {
        if (options.InputWidth.HasValue && options.InputHeight.HasValue)
        {
            return (options.InputWidth.Value, options.InputHeight.Value);
        }

        try
        {
            var inputMetadata = session.InputMetadata[session.InputNames[0]];
            var shape = inputMetadata.Dimensions;
            if (shape.Length == 4)
            {
                int height = shape[2];
                int width = shape[3];
                if (height > 0 && width > 0)
                {
                    return (width, height);
                }
            }
        }
        catch
        {
        }

        return (640, 640);
    }

    private static (DenseTensor<float> tensor, float ratio, int padW, int padH) PreprocessImage(
        Mat image,
        int targetWidth,
        int targetHeight
    )
    {
        float ratio = Math.Min((float)targetWidth / image.Width, (float)targetHeight / image.Height);
        int newWidth = (int)(image.Width * ratio);
        int newHeight = (int)(image.Height * ratio);
        int padW = (targetWidth - newWidth) / 2;
        int padH = (targetHeight - newHeight) / 2;

        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(newWidth, newHeight), interpolation: InterpolationFlags.Linear);

        using var padded = new Mat(targetHeight, targetWidth, MatType.CV_8UC3, new Scalar(114, 114, 114));
        var roi = new Rect(padW, padH, newWidth, newHeight);
        resized.CopyTo(new Mat(padded, roi));

        using var rgb = new Mat();
        Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);

        var tensor = new DenseTensor<float>([1, 3, targetHeight, targetWidth]);
        unsafe
        {
            byte* ptr = (byte*)rgb.DataPointer;
            int channels = rgb.Channels();

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int idx = (y * targetWidth + x) * channels;
                    tensor[0, 0, y, x] = ptr[idx] / 255f;
                    tensor[0, 1, y, x] = ptr[idx + 1] / 255f;
                    tensor[0, 2, y, x] = ptr[idx + 2] / 255f;
                }
            }
        }

        return (tensor, ratio, padW, padH);
    }

    private static List<HbbDetectionResult> PostProcess(
        Tensor<float> outputTensor,
        int[] dims,
        float ratio,
        int padW,
        int padH,
        DetectionOptions options,
        string[] labels,
        int originalWidth,
        int originalHeight
    )
    {
        var detections = new List<HbbDetectionResult>();

        int numFeatures = dims[1];
        int numPredictions = dims[2];
        int numClasses = numFeatures - 4;
        if (numClasses <= 0)
        {
            numClasses = labels.Length;
        }

        for (int i = 0; i < numPredictions; i++)
        {
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
            {
                continue;
            }

            float cx = outputTensor[0, 0, i];
            float cy = outputTensor[0, 1, i];
            float bw = outputTensor[0, 2, i];
            float bh = outputTensor[0, 3, i];

            float x1 = (cx - bw / 2 - padW) / ratio;
            float y1 = (cy - bh / 2 - padH) / ratio;
            float x2 = (cx + bw / 2 - padW) / ratio;
            float y2 = (cy + bh / 2 - padH) / ratio;

            x1 = Math.Max(0, x1);
            y1 = Math.Max(0, y1);
            x2 = Math.Min(originalWidth, x2);
            y2 = Math.Min(originalHeight, y2);

            if (x2 <= x1 || y2 <= y1)
            {
                continue;
            }

            detections.Add(
                new HbbDetectionResult
                {
                    LabelIndex = maxIndex,
                    LabelName = maxIndex < labels.Length ? labels[maxIndex] : $"class_{maxIndex}",
                    Confidence = maxScore,
                    BoundingBox = new BoundingBox(
                        (int)x1,
                        (int)y1,
                        (int)(x2 - x1),
                        (int)(y2 - y1)
                    ),
                }
            );
        }

        return ApplyNms(detections, options.NmsThreshold);
    }

    private static List<HbbDetectionResult> ApplyNms(
        List<HbbDetectionResult> detections,
        float nmsThreshold
    )
    {
        var result = new List<HbbDetectionResult>();
        var sorted = detections.OrderByDescending(static detection => detection.Confidence).ToList();

        while (sorted.Count > 0)
        {
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);

            sorted = [.. sorted.Where(detection =>
            {
                if (detection.LabelIndex != best.LabelIndex)
                {
                    return true;
                }

                float iou = CalculateIoU(best.BoundingBox, detection.BoundingBox);
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
}
