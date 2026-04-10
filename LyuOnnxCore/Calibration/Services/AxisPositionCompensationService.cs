using LyuOnnxCore.Calibration.Interface;
using LyuOnnxCore.Calibration.Models;
using OpenCvSharp;

namespace LyuOnnxCore.Calibration.Services;

internal sealed class AxisPositionCompensationService : IAxisPositionCompensation
{
    public Point2d TransformToCalibrationPixel(
        Point2d detectedPixel,
        Point2d currentAxisPosition,
        Point2d calibrationAxisZero,
        NinePointCalibrationResult ninePointCalibration
    )
    {
        ArgumentNullException.ThrowIfNull(ninePointCalibration);
        return CompensatePixelInternal(
            detectedPixel,
            currentAxisPosition,
            calibrationAxisZero,
            ninePointCalibration
        );
    }

    public Point2d PixelToWorldWithCompensation(
        Point2d detectedPixel,
        Point2d currentAxisPosition,
        Point2d calibrationAxisZero,
        NinePointCalibrationResult ninePointCalibration
    )
    {
        ArgumentNullException.ThrowIfNull(ninePointCalibration);
        var calibrationPixel = TransformToCalibrationPixel(
            detectedPixel,
            currentAxisPosition,
            calibrationAxisZero,
            ninePointCalibration
        );
        using var pixelToWorldTransform = CreateMatrixMat(ninePointCalibration.PixelToWorldTransform);
        return Cv2.PerspectiveTransform([calibrationPixel], pixelToWorldTransform)[0];
    }

    private Point2d CompensatePixelInternal(
        Point2d detectedPixel,
        Point2d currentAxisPosition,
        Point2d calibrationAxisZero,
        NinePointCalibrationResult ninePointCalibration
    )
    {
        var calibrationPlanePixel = UndistortPointIfNeeded(detectedPixel, ninePointCalibration);
        using var pixelToWorldTransform = CreateMatrixMat(ninePointCalibration.PixelToWorldTransform);
        var currentWorld = Cv2.PerspectiveTransform([calibrationPlanePixel], pixelToWorldTransform)[0];

        var axisOffset = new Point2d(
            currentAxisPosition.X - calibrationAxisZero.X,
            currentAxisPosition.Y - calibrationAxisZero.Y
        );

        var compensatedWorld = new Point2d(
            currentWorld.X + axisOffset.X,
            currentWorld.Y + axisOffset.Y
        );

        using var worldToPixelTransform = CreateMatrixMat(ninePointCalibration.WorldToPixelTransform);
        return Cv2.PerspectiveTransform([compensatedWorld], worldToPixelTransform)[0];
    }

    private static Point2d UndistortPointIfNeeded(
        Point2d point,
        NinePointCalibrationResult ninePointCalibration
    )
    {
        if (ninePointCalibration.CameraMatrix is null || ninePointCalibration.DistortionCoefficients.Length == 0)
        {
            return point;
        }

        using var source = new Mat(1, 1, MatType.CV_64FC2);
        source.Set(0, 0, point);

        using var destination = new Mat();
        using var cameraMatrix = CreateMatrixMat(ninePointCalibration.CameraMatrix);
        using var distortion = CreateColumnMat(ninePointCalibration.DistortionCoefficients);

        using var empty = new Mat();
        Cv2.UndistortPoints(source, destination, cameraMatrix, distortion, empty, cameraMatrix);
        return destination.Get<Point2d>(0, 0);
    }

    private static Mat CreateMatrixMat(double[,] values)
    {
        ArgumentNullException.ThrowIfNull(values);

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
        ArgumentNullException.ThrowIfNull(values);

        var mat = new Mat(values.Count, 1, MatType.CV_64FC1);

        for (int i = 0; i < values.Count; i++)
        {
            mat.Set(i, 0, values[i]);
        }

        return mat;
    }
}
