using LyuOnnxCore.Models;
using OpenCvSharp;
using System.IO;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// OpenCV Mat helpers.
/// </summary>
public static class MatExtensions
{
    public static List<string> SaveCroppedRegions(
        this Mat image,
        IReadOnlyList<HbbDetectionResult> detections,
        string outputFolder,
        out List<string> errorMessages,
        string fileNamePrefix = "crop"
    )
    {
        return SaveCroppedRegionsCore(
            image,
            detections,
            outputFolder,
            out errorMessages,
            detection => CropBoundingBox(image, detection.BoundingBox, out _),
            detection => detection.LabelName,
            detection => detection.Confidence,
            fileNamePrefix
        );
    }

    public static List<string> SaveCroppedRegions(
        this Mat image,
        IReadOnlyList<ObbDetectionResult> detections,
        string outputFolder,
        out List<string> errorMessages,
        string fileNamePrefix = "crop"
    )
    {
        return SaveCroppedRegionsCore(
            image,
            detections,
            outputFolder,
            out errorMessages,
            detection => CropRotatedRect(image, detection.OrientedBoundingBox, out _),
            detection => detection.LabelName,
            detection => detection.Confidence,
            fileNamePrefix
        );
    }

    public static bool SaveCroppedRegion(
        this Mat image,
        HbbDetectionResult detection,
        string filePath
    )
    {
        return SaveCroppedRegionCore(
            image,
            filePath,
            () => CropBoundingBox(image, detection.BoundingBox, out _)
        );
    }

    public static bool SaveCroppedRegion(
        this Mat image,
        ObbDetectionResult detection,
        string filePath
    )
    {
        return SaveCroppedRegionCore(
            image,
            filePath,
            () => CropRotatedRect(image, detection.OrientedBoundingBox, out _)
        );
    }

    private static List<string> SaveCroppedRegionsCore<TDetection>(
        Mat image,
        IReadOnlyList<TDetection> detections,
        string outputFolder,
        out List<string> errorMessages,
        Func<TDetection, Mat?> cropFactory,
        Func<TDetection, string> labelSelector,
        Func<TDetection, float> confidenceSelector,
        string fileNamePrefix
    )
    {
        errorMessages = [];

        if (image is null || image.Empty())
        {
            throw new ArgumentException("Image cannot be null or empty.", nameof(image));
        }

        if (detections is null || detections.Count == 0)
        {
            return [];
        }

        Directory.CreateDirectory(outputFolder);

        var savedFiles = new List<string>();
        for (int index = 0; index < detections.Count; index++)
        {
            var detection = detections[index];
            try
            {
                using var croppedMat = cropFactory(detection);
                if (croppedMat is null || croppedMat.Empty())
                {
                    errorMessages.Add($"Index {index} ({labelSelector(detection)}): crop result is empty.");
                    continue;
                }

                string fileName =
                    $"{fileNamePrefix}_{labelSelector(detection)}_{index}_{confidenceSelector(detection):F2}.jpg";
                string filePath = Path.Combine(outputFolder, fileName);
                Cv2.ImWrite(filePath, croppedMat);
                savedFiles.Add(filePath);
            }
            catch (Exception ex)
            {
                errorMessages.Add($"Index {index} ({labelSelector(detection)}): {ex.Message}");
            }
        }

        return savedFiles;
    }

    private static bool SaveCroppedRegionCore(
        Mat image,
        string filePath,
        Func<Mat?> cropFactory
    )
    {
        if (image is null || image.Empty())
        {
            throw new ArgumentException("Image cannot be null or empty.", nameof(image));
        }

        try
        {
            using var croppedMat = cropFactory();
            if (croppedMat is null || croppedMat.Empty())
            {
                return false;
            }

            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Cv2.ImWrite(filePath, croppedMat);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Mat? CropBoundingBox(
        Mat image,
        BoundingBox box,
        out string debugInfo
    )
    {
        int x1 = Math.Max(0, box.X);
        int y1 = Math.Max(0, box.Y);
        int x2 = Math.Min(image.Width, box.X + box.Width);
        int y2 = Math.Min(image.Height, box.Y + box.Height);
        int width = x2 - x1;
        int height = y2 - y1;

        if (width <= 0 || height <= 0)
        {
            debugInfo = $"Invalid bounding box size: width={width}, height={height}.";
            return null;
        }

        debugInfo = "Success";
        return new Mat(image, new Rect(x1, y1, width, height));
    }

    private static Mat? CropRotatedRect(
        Mat image,
        OrientedBoundingBox obb,
        out string debugInfo
    )
    {
        debugInfo = string.Empty;
        try
        {
            var corners = obb.GetCornerPoints();
            float actualWidth = MathF.Sqrt(
                MathF.Pow(corners[1].X - corners[0].X, 2) +
                MathF.Pow(corners[1].Y - corners[0].Y, 2)
            );
            float actualHeight = MathF.Sqrt(
                MathF.Pow(corners[3].X - corners[0].X, 2) +
                MathF.Pow(corners[3].Y - corners[0].Y, 2)
            );

            if (actualWidth <= 0 || actualHeight <= 0)
            {
                debugInfo = $"Invalid OBB size: W={actualWidth:F2}, H={actualHeight:F2}.";
                return null;
            }

            int outputWidth = (int)Math.Round(actualWidth);
            int outputHeight = (int)Math.Round(actualHeight);

            var srcPoints = new Point2f[]
            {
                new(corners[0].X, corners[0].Y),
                new(corners[1].X, corners[1].Y),
                new(corners[2].X, corners[2].Y),
                new(corners[3].X, corners[3].Y),
            };

            var dstPoints = new Point2f[]
            {
                new(0, 0),
                new(outputWidth - 1, 0),
                new(outputWidth - 1, outputHeight - 1),
                new(0, outputHeight - 1),
            };

            using var transformMatrix = Cv2.GetPerspectiveTransform(srcPoints, dstPoints);

            var result = new Mat();
            Cv2.WarpPerspective(
                image,
                result,
                transformMatrix,
                new Size(outputWidth, outputHeight),
                InterpolationFlags.Linear,
                BorderTypes.Constant,
                new Scalar(0, 0, 0)
            );

            if (result.Empty())
            {
                debugInfo = "Crop result is empty after perspective transform.";
                result.Dispose();
                return null;
            }

            debugInfo = "Success";
            return result;
        }
        catch (Exception ex)
        {
            debugInfo = ex.Message;
            return null;
        }
    }
}
