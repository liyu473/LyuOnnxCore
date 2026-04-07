using OpenCvSharp;

namespace LyuOnnxCore.Calibration;

/// <summary>
/// Maps image points onto a known planar board coordinate system.
/// </summary>
public sealed class BoardCoordinateMapper : IDisposable
{
    private readonly Mat _boardFromImageTransform;
    private readonly Mat _imageFromBoardTransform;
    private readonly CameraCalibrationResult? _calibration;

    private BoardCoordinateMapper(Mat boardFromImageTransform, Mat imageFromBoardTransform, CameraCalibrationResult? calibration)
    {
        _boardFromImageTransform = boardFromImageTransform;
        _imageFromBoardTransform = imageFromBoardTransform;
        _calibration = calibration;
    }

    public static BoardCoordinateMapper Create(
        IEnumerable<Point2d> imagePoints,
        IEnumerable<Point2d> boardPoints,
        CameraCalibrationResult? calibration = null,
        HomographyMethods homographyMethod = HomographyMethods.Ransac,
        double ransacReprojectionThreshold = 3.0)
    {
        ArgumentNullException.ThrowIfNull(imagePoints);
        ArgumentNullException.ThrowIfNull(boardPoints);

        var imagePointArray = imagePoints.ToArray();
        var boardPointArray = boardPoints.ToArray();

        if (imagePointArray.Length != boardPointArray.Length)
            throw new InvalidOperationException("Image points and board points must have the same count.");

        if (imagePointArray.Length < 4)
            throw new InvalidOperationException("At least 4 point correspondences are required to estimate a planar transform.");

        var normalizedImagePoints = calibration is null
            ? imagePointArray
            : UndistortPoints(imagePointArray, calibration);

        using var inlierMask = new Mat();
        var boardFromImageTransform = Cv2.FindHomography(
            normalizedImagePoints,
            boardPointArray,
            homographyMethod,
            ransacReprojectionThreshold,
            inlierMask);

        if (boardFromImageTransform.Empty())
            throw new InvalidOperationException("Failed to estimate the planar transform.");

        using var imageFromBoardTransform = new Mat();
        if (Cv2.Invert(boardFromImageTransform, imageFromBoardTransform) == 0)
            throw new InvalidOperationException("Failed to invert the planar transform.");

        return new BoardCoordinateMapper(boardFromImageTransform, imageFromBoardTransform.Clone(), calibration);
    }

    public static BoardCoordinateMapper CreateFromChessboard(
        Mat image,
        ChessboardCalibrationBoard board,
        CameraCalibrationResult? calibration = null,
        ChessboardDetectionOptions? detectionOptions = null,
        HomographyMethods homographyMethod = HomographyMethods.Ransac,
        double ransacReprojectionThreshold = 3.0)
    {
        var detection = CameraCalibrationService.DetectChessboardCorners(image, board, detectionOptions);
        if (!detection.IsSuccess || detection.Corners.Length != board.CornerCount)
            throw new InvalidOperationException("Failed to detect a full chessboard in the current image.");

        var imagePoints = detection.Corners
            .Select(static point => new Point2d(point.X, point.Y));

        return Create(
            imagePoints,
            board.CreatePlanarPoints(),
            calibration,
            homographyMethod,
            ransacReprojectionThreshold);
    }

    public Point2d ImageToBoard(Point2d imagePoint)
    {
        var sourcePoint = _calibration is null
            ? imagePoint
            : UndistortPoints([imagePoint], _calibration)[0];

        return Cv2.PerspectiveTransform([sourcePoint], _boardFromImageTransform)[0];
    }

    public Point2d[] ImageToBoard(IEnumerable<Point2d> imagePoints)
    {
        ArgumentNullException.ThrowIfNull(imagePoints);

        var imagePointArray = imagePoints.ToArray();
        var sourcePoints = _calibration is null
            ? imagePointArray
            : UndistortPoints(imagePointArray, _calibration);

        return Cv2.PerspectiveTransform(sourcePoints, _boardFromImageTransform);
    }

    public Point2d BoardToImage(Point2d boardPoint)
    {
        return Cv2.PerspectiveTransform([boardPoint], _imageFromBoardTransform)[0];
    }

    public Point2d[] BoardToImage(IEnumerable<Point2d> boardPoints)
    {
        ArgumentNullException.ThrowIfNull(boardPoints);
        return Cv2.PerspectiveTransform(boardPoints.ToArray(), _imageFromBoardTransform);
    }

    public void Dispose()
    {
        _boardFromImageTransform.Dispose();
        _imageFromBoardTransform.Dispose();
    }

    private static Point2d[] UndistortPoints(IReadOnlyList<Point2d> points, CameraCalibrationResult calibration)
    {
        if (points.Count == 0)
            return [];

        using var source = new Mat(points.Count, 1, MatType.CV_64FC2);
        for (int i = 0; i < points.Count; i++)
        {
            source.Set(i, 0, points[i]);
        }

        using var destination = new Mat();
        using var cameraMatrix = CreateMatrixMat(calibration.CameraMatrix);
        using var distortion = CreateColumnMat(calibration.DistortionCoefficients);
        using var empty = new Mat();

        Cv2.UndistortPoints(source, destination, cameraMatrix, distortion, empty, empty);

        var undistorted = new Point2d[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            undistorted[i] = destination.Get<Point2d>(i, 0);
        }

        return undistorted;
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
