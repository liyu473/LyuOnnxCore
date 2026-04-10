using LyuOnnxCore.Calibration.Interface;
using LyuOnnxCore.Calibration.Models;
using LyuOnnxCore.Calibration.Serialization;
using OpenCvSharp;
using System.Text.Json;

namespace LyuOnnxCore.Calibration.Services;

internal sealed class NinePointCalibrationService : INinePointCalibration
{
    public NinePointCalibrationResult Calibrate(
        CameraCalibrateResult cameraCalibration,
        IList<CalibrationPair> pairs
    )
    {
        ArgumentNullException.ThrowIfNull(cameraCalibration);
        ArgumentNullException.ThrowIfNull(pairs);

        if (pairs.Count < 4)
        {
            throw new InvalidOperationException("Nine-point calibration requires at least 4 point pairs, and 9 or more is recommended.");
        }

        var rawImagePoints = pairs.Select(static pair => pair.ImagePoint).ToArray();
        var undistortedImagePoints = UndistortPoints(rawImagePoints, cameraCalibration);
        var worldPoints = pairs.Select(static pair => pair.WorldPoint).ToArray();

        using var inlierMask = new Mat();
        using var pixelToWorldMat = Cv2.FindHomography(
            undistortedImagePoints,
            worldPoints,
            HomographyMethods.Ransac,
            3.0,
            inlierMask
        );

        if (pixelToWorldMat.Empty())
        {
            throw new InvalidOperationException("Failed to solve the pixel-to-world transform from the provided point pairs.");
        }

        using var worldToPixelMat = new Mat();
        if (Cv2.Invert(pixelToWorldMat, worldToPixelMat) == 0)
        {
            throw new InvalidOperationException("Failed to invert the world-to-pixel transform.");
        }

        var projectedWorldPoints = Cv2.PerspectiveTransform(undistortedImagePoints, pixelToWorldMat);
        CalculateErrors(projectedWorldPoints, worldPoints, out double meanError, out double maxError);

        return new NinePointCalibrationResult
        {
            PixelToWorldTransform = ToDoubleMatrix(pixelToWorldMat),
            WorldToPixelTransform = ToDoubleMatrix(worldToPixelMat),
            CameraMatrix = (double[,])cameraCalibration.CameraMatrix.Clone(),
            DistortionCoefficients = [.. cameraCalibration.DistortionCoefficients],
            MeanReprojectionError = meanError,
            MaxReprojectionError = maxError,
            PairCount = pairs.Count,
            InlierCount = CountInliers(inlierMask, pairs.Count),
            Pairs = [.. pairs]
        };
    }

    public Point2d PixelToWorld(
        Point2d pixelPoint,
        CameraCalibrateResult cameraCalibration,
        NinePointCalibrationResult ninePointCalibration
    )
    {
        ArgumentNullException.ThrowIfNull(cameraCalibration);
        ArgumentNullException.ThrowIfNull(ninePointCalibration);

        var undistortedPoint = UndistortPoint(pixelPoint, cameraCalibration);
        using var pixelToWorldTransform = CreateMatrixMat(ninePointCalibration.PixelToWorldTransform);
        return Cv2.PerspectiveTransform([undistortedPoint], pixelToWorldTransform)[0];
    }

    public string SerializeResult(
        NinePointCalibrationResult result,
        bool writeIndented = true
    )
    {
        ArgumentNullException.ThrowIfNull(result);

        return JsonSerializer.Serialize(
            result,
            CalibrationJsonSerializer.CreateOptions(writeIndented)
        );
    }

    public NinePointCalibrationResult DeserializeResult(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Calibration result JSON cannot be null or empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<NinePointCalibrationResult>(
            json,
            CalibrationJsonSerializer.CreateOptions(writeIndented: false)
        ) ?? throw new InvalidOperationException("Failed to deserialize nine-point calibration result.");
    }

    private static Point2d UndistortPoint(Point2d point, CameraCalibrateResult cameraCalibration)
    {
        return UndistortPoints([point], cameraCalibration)[0];
    }

    private static Point2d[] UndistortPoints(
        IReadOnlyList<Point2d> points,
        CameraCalibrateResult cameraCalibration
    )
    {
        if (points.Count == 0)
        {
            return [];
        }

        using var source = new Mat(points.Count, 1, MatType.CV_64FC2);
        for (int i = 0; i < points.Count; i++)
        {
            source.Set(i, 0, points[i]);
        }

        using var destination = new Mat();
        using var cameraMatrix = CreateMatrixMat(cameraCalibration.CameraMatrix);
        using var distortion = CreateColumnMat(cameraCalibration.DistortionCoefficients);

        using var empty = new Mat();
        Cv2.UndistortPoints(source, destination, cameraMatrix, distortion, empty, cameraMatrix);

        var undistorted = new Point2d[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            undistorted[i] = destination.Get<Point2d>(i, 0);
        }

        return undistorted;
    }

    private static int CountInliers(Mat inlierMask, int fallback)
    {
        if (inlierMask.Empty())
        {
            return fallback;
        }

        int count = 0;
        for (int row = 0; row < inlierMask.Rows; row++)
        {
            if (inlierMask.Get<byte>(row, 0) != 0)
            {
                count++;
            }
        }

        return count;
    }

    private static void CalculateErrors(
        IReadOnlyList<Point2d> projectedPoints,
        IReadOnlyList<Point2d> worldPoints,
        out double meanError,
        out double maxError
    )
    {
        double totalError = 0;
        double maxDistance = 0;

        for (int i = 0; i < projectedPoints.Count; i++)
        {
            double dx = projectedPoints[i].X - worldPoints[i].X;
            double dy = projectedPoints[i].Y - worldPoints[i].Y;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));
            totalError += distance;
            maxDistance = Math.Max(maxDistance, distance);
        }

        meanError = projectedPoints.Count == 0 ? 0 : totalError / projectedPoints.Count;
        maxError = maxDistance;
    }

    private static double[,] ToDoubleMatrix(Mat mat)
    {
        var values = new double[mat.Rows, mat.Cols];
        for (int row = 0; row < mat.Rows; row++)
        {
            for (int column = 0; column < mat.Cols; column++)
            {
                values[row, column] = mat.Get<double>(row, column);
            }
        }

        return values;
    }

    private static Mat CreateMatrixMat(double[,] values)
    {
        var rowCount = values.GetLength(0);
        var columnCount = values.GetLength(1);
        var mat = new Mat(rowCount, columnCount, MatType.CV_64FC1);

        for (int row = 0; row < rowCount; row++)
        {
            for (int column = 0; column < columnCount; column++)
            {
                mat.Set(row, column, values[row, column]);
            }
        }

        return mat;
    }

    private static Mat CreateColumnMat(IReadOnlyList<double> values)
    {
        var mat = new Mat(values.Count, 1, MatType.CV_64FC1);

        for (int i = 0; i < values.Count; i++)
        {
            mat.Set(i, 0, values[i]);
        }

        return mat;
    }
}
