using LyuOnnxCore.Helpers;
using LyuOnnxCore.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// Oriented bounding box detection helpers for OpenCV Mat.
/// </summary>
public static class MatObbDetectionExtensions
{
    public static List<ObbDetectionResult> DetectOBB(
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

        var results = PostProcessObb(outputTensor, dims, ratio, padW, padH, options, labels);

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

    public static Mat DrawOBBDetections(
        this Mat image,
        IEnumerable<ObbDetectionResult> detections,
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
            var obb = detection.OrientedBoundingBox;
            var corners = obb.GetCornerPoints();
            var color = new Scalar(options.BoxColor.B, options.BoxColor.G, options.BoxColor.R);

            for (int i = 0; i < 4; i++)
            {
                var pt1 = new Point((int)corners[i].X, (int)corners[i].Y);
                var pt2 = new Point((int)corners[(i + 1) % 4].X, (int)corners[(i + 1) % 4].Y);
                Cv2.Line(result, pt1, pt2, color, options.BoxThickness, LineTypes.AntiAlias);
            }

            string angleInfo = $"{obb.AngleDegrees:F1}deg";
            string label = options switch
            {
                { ShowLabel: true, ShowConfidence: true } => $"[{index}] {detection.LabelName} {detection.Confidence:P0} {angleInfo}",
                { ShowLabel: true } => $"[{index}] {detection.LabelName} {angleInfo}",
                { ShowConfidence: true } => $"[{index}] {detection.Confidence:P0} {angleInfo}",
                _ => $"[{index}] {angleInfo}",
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
                    new Point((int)corners[0].X, (int)corners[0].Y),
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

                var textPos = new Point((int)corners[0].X, (int)corners[0].Y - 5);
                var textRect = new Rect(
                    textPos.X,
                    textPos.Y - textSize.Height - baseline,
                    textSize.Width + 5,
                    textSize.Height + baseline + 5
                );

                Cv2.Rectangle(result, textRect, color, -1);
                Cv2.PutText(
                    result,
                    label,
                    new Point(textPos.X + 2, textPos.Y - baseline),
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

    public static Mat DetectOBBAndDraw(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? detectionOptions = null,
        DrawOptions? drawOptions = null
    )
    {
        var results = session.DetectOBB(image, labels, detectionOptions);
        return image.DrawOBBDetections(results, drawOptions);
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

    private static List<ObbDetectionResult> PostProcessObb(
        Tensor<float> outputTensor,
        int[] dims,
        float ratio,
        int padW,
        int padH,
        DetectionOptions options,
        string[] labels
    )
    {
        var detections = new List<ObbDetectionResult>();

        int numFeatures = dims[1];
        int numPredictions = dims[2];
        int numClasses = numFeatures - 4 - 1;
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
            float width = outputTensor[0, 2, i] / ratio;
            float height = outputTensor[0, 3, i] / ratio;
            float angle = outputTensor[0, 4 + numClasses, i];

            if (width <= 0 || height <= 0)
            {
                continue;
            }

            detections.Add(
                new ObbDetectionResult
                {
                    LabelIndex = maxIndex,
                    LabelName = maxIndex < labels.Length ? labels[maxIndex] : $"class_{maxIndex}",
                    Confidence = maxScore,
                    OrientedBoundingBox = new OrientedBoundingBox(
                        (cx - padW) / ratio,
                        (cy - padH) / ratio,
                        width,
                        height,
                        angle
                    ),
                }
            );
        }

        return ApplyNmsObb(detections, options.NmsThreshold);
    }

    private static List<ObbDetectionResult> ApplyNmsObb(
        List<ObbDetectionResult> detections,
        float nmsThreshold
    )
    {
        var result = new List<ObbDetectionResult>();
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

                float iou = CalculateObbIoU(best.OrientedBoundingBox, detection.OrientedBoundingBox);
                return iou < nmsThreshold;
            })];
        }

        return result;
    }

    private static float CalculateObbIoU(OrientedBoundingBox obb1, OrientedBoundingBox obb2)
    {
        var rect1 = new RotatedRect(
            new Point2f(obb1.CenterX, obb1.CenterY),
            new Size2f(obb1.Width, obb1.Height),
            obb1.AngleDegrees
        );

        var rect2 = new RotatedRect(
            new Point2f(obb2.CenterX, obb2.CenterY),
            new Size2f(obb2.Width, obb2.Height),
            obb2.AngleDegrees
        );

        using var intersectionPoints = new Mat();
        var intersectionType = Cv2.RotatedRectangleIntersection(rect1, rect2, intersectionPoints);

        float intersectionArea = 0;
        if (intersectionType == RectanglesIntersectTypes.Full)
        {
            intersectionArea = Math.Min(rect1.Size.Width * rect1.Size.Height, rect2.Size.Width * rect2.Size.Height);
        }
        else if (intersectionType != RectanglesIntersectTypes.None && !intersectionPoints.Empty() && intersectionPoints.Rows >= 3)
        {
            var points = new Point2f[intersectionPoints.Rows];
            for (int i = 0; i < intersectionPoints.Rows; i++)
            {
                points[i] = new Point2f(intersectionPoints.At<float>(i, 0), intersectionPoints.At<float>(i, 1));
            }

            intersectionArea = (float)Math.Abs(Cv2.ContourArea(points));
        }

        float area1 = rect1.Size.Width * rect1.Size.Height;
        float area2 = rect2.Size.Width * rect2.Size.Height;
        float unionArea = area1 + area2 - intersectionArea;

        return unionArea > 0 ? intersectionArea / unionArea : 0;
    }
}
