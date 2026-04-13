using LyuOnnxCore.Extensions;
using LyuOnnxCore.Interfaces;
using LyuOnnxCore.Models;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace LyuOnnxCore.Services;

internal sealed class YoloObbDetectionService : IYoloObbDetectionService
{
    public ObbDetectionResult[] Detect(
        string modelPath,
        string imagePath,
        IEnumerable<string> labels,
        DetectionOptions? detectionOptions = null
    )
    {
        ValidateFilePath(imagePath, nameof(imagePath));

        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            throw new InvalidOperationException("Failed to load the image.");
        }

        return Detect(modelPath, image, labels, detectionOptions);
    }

    public ObbDetectionResult[] Detect(
        string modelPath,
        Mat image,
        IEnumerable<string> labels,
        DetectionOptions? detectionOptions = null
    )
    {
        ValidateFilePath(modelPath, nameof(modelPath));

        if (image is null || image.Empty())
        {
            throw new ArgumentException("Image cannot be null or empty.", nameof(image));
        }

        var labelArray = labels?
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .ToArray()
            ?? [];

        if (labelArray.Length == 0)
        {
            throw new ArgumentException("At least one label is required.", nameof(labels));
        }

        using var session = new InferenceSession(modelPath);
        return [.. session.DetectOBB(image, labelArray, detectionOptions)];
    }

    public Mat DetectAndVisualize(
        string modelPath,
        string imagePath,
        IEnumerable<string> labels,
        DetectionOptions? detectionOptions = null,
        DrawOptions? drawOptions = null
    )
    {
        ValidateFilePath(imagePath, nameof(imagePath));

        using var image = Cv2.ImRead(imagePath, ImreadModes.Color);
        if (image.Empty())
        {
            throw new InvalidOperationException("Failed to load the image.");
        }

        return DetectAndVisualize(modelPath, image, labels, detectionOptions, drawOptions);
    }

    public Mat DetectAndVisualize(
        string modelPath,
        Mat image,
        IEnumerable<string> labels,
        DetectionOptions? detectionOptions = null,
        DrawOptions? drawOptions = null
    )
    {
        ValidateFilePath(modelPath, nameof(modelPath));

        if (image is null || image.Empty())
        {
            throw new ArgumentException("Image cannot be null or empty.", nameof(image));
        }

        var labelArray = labels?
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .ToArray()
            ?? [];

        if (labelArray.Length == 0)
        {
            throw new ArgumentException("At least one label is required.", nameof(labels));
        }

        using var session = new InferenceSession(modelPath);
        return session.DetectOBBAndDraw(image, labelArray, detectionOptions, drawOptions);
    }

    private static void ValidateFilePath(string path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("The path cannot be null or whitespace.", paramName);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File was not found: {path}", path);
        }
    }
}
