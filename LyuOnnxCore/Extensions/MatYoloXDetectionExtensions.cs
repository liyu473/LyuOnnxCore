using LyuOnnxCore.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// YOLOX horizontal bounding box detection helpers for OpenCV Mat.
/// </summary>
public static class MatYoloXDetectionExtensions
{
    public static List<HbbDetectionResult> DetectYoloX(
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
        var (inputTensor, ratio) = PreprocessImage(image, inputWidth, inputHeight);

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(session.InputNames[0], inputTensor)
        };

        using var outputs = session.Run(inputs);
        var outputTensor = outputs.ElementAt(0).AsTensor<float>();
        var dims = outputTensor.Dimensions.ToArray();

        var results = PostProcessYoloX(
            outputTensor,
            dims,
            ratio,
            inputWidth,
            inputHeight,
            options,
            labels,
            image.Width,
            image.Height
        );

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

    public static Mat DetectYoloXAndDraw(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? detectionOptions = null,
        DrawOptions? drawOptions = null
    )
    {
        var results = session.DetectYoloX(image, labels, detectionOptions);
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

    private static (DenseTensor<float> tensor, float ratio) PreprocessImage(
        Mat image,
        int targetWidth,
        int targetHeight
    )
    {
        float ratio = Math.Min((float)targetWidth / image.Width, (float)targetHeight / image.Height);
        int resizedWidth = (int)(image.Width * ratio);
        int resizedHeight = (int)(image.Height * ratio);

        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(resizedWidth, resizedHeight), interpolation: InterpolationFlags.Linear);

        using var padded = new Mat(targetHeight, targetWidth, MatType.CV_8UC3, new Scalar(114, 114, 114));
        var roi = new Rect(0, 0, resizedWidth, resizedHeight);
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

        return (tensor, ratio);
    }

    private static List<HbbDetectionResult> PostProcessYoloX(
        Tensor<float> outputTensor,
        int[] dims,
        float ratio,
        int inputWidth,
        int inputHeight,
        DetectionOptions options,
        string[] labels,
        int originalWidth,
        int originalHeight
    )
    {
        var (numPredictions, numFeatures, getValue) = CreateTensorAccessor(outputTensor, dims);
        int numClasses = numFeatures - 5;
        if (numClasses <= 0)
        {
            throw new InvalidOperationException("YOLOX output does not contain class scores.");
        }

        var decoded = DecodePredictions(getValue, numPredictions, numFeatures, inputWidth, inputHeight);
        var detections = new List<HbbDetectionResult>();

        for (int i = 0; i < numPredictions; i++)
        {
            float objectness = decoded[i, 4];
            if (objectness <= 0)
            {
                continue;
            }

            float maxScore = 0;
            int maxIndex = 0;
            for (int c = 0; c < numClasses; c++)
            {
                float score = objectness * decoded[i, 5 + c];
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

            float cx = decoded[i, 0];
            float cy = decoded[i, 1];
            float bw = decoded[i, 2];
            float bh = decoded[i, 3];

            float x1 = (cx - bw / 2f) / ratio;
            float y1 = (cy - bh / 2f) / ratio;
            float x2 = (cx + bw / 2f) / ratio;
            float y2 = (cy + bh / 2f) / ratio;

            x1 = Math.Clamp(x1, 0, originalWidth);
            y1 = Math.Clamp(y1, 0, originalHeight);
            x2 = Math.Clamp(x2, 0, originalWidth);
            y2 = Math.Clamp(y2, 0, originalHeight);

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

    private static (int numPredictions, int numFeatures, Func<int, int, float> getValue) CreateTensorAccessor(
        Tensor<float> outputTensor,
        int[] dims
    )
    {
        if (dims.Length == 3)
        {
            if (dims[1] > dims[2])
            {
                return (dims[1], dims[2], (predictionIndex, featureIndex) => outputTensor[0, predictionIndex, featureIndex]);
            }

            return (dims[2], dims[1], (predictionIndex, featureIndex) => outputTensor[0, featureIndex, predictionIndex]);
        }

        if (dims.Length == 2)
        {
            if (dims[0] > dims[1])
            {
                return (dims[0], dims[1], (predictionIndex, featureIndex) => outputTensor[predictionIndex, featureIndex]);
            }

            return (dims[1], dims[0], (predictionIndex, featureIndex) => outputTensor[featureIndex, predictionIndex]);
        }

        throw new NotSupportedException($"Unsupported YOLOX output dimensions: [{string.Join(", ", dims)}]");
    }

    private static float[,] DecodePredictions(
        Func<int, int, float> getValue,
        int numPredictions,
        int numFeatures,
        int inputWidth,
        int inputHeight
    )
    {
        var outputs = new float[numPredictions, numFeatures];
        for (int i = 0; i < numPredictions; i++)
        {
            for (int f = 0; f < numFeatures; f++)
            {
                outputs[i, f] = getValue(i, f);
            }
        }

        var strides = new List<int> { 8, 16, 32 };
        int totalGridCount = 0;
        foreach (var stride in strides)
        {
            totalGridCount += (inputHeight / stride) * (inputWidth / stride);
        }

        if (totalGridCount != numPredictions)
        {
            return outputs;
        }

        int index = 0;
        foreach (var stride in strides)
        {
            int gridHeight = inputHeight / stride;
            int gridWidth = inputWidth / stride;

            for (int gy = 0; gy < gridHeight; gy++)
            {
                for (int gx = 0; gx < gridWidth; gx++)
                {
                    outputs[index, 0] = (outputs[index, 0] + gx) * stride;
                    outputs[index, 1] = (outputs[index, 1] + gy) * stride;
                    outputs[index, 2] = MathF.Exp(outputs[index, 2]) * stride;
                    outputs[index, 3] = MathF.Exp(outputs[index, 3]) * stride;
                    index++;
                }
            }
        }

        return outputs;
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
