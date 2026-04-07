using OpenCvSharp;

namespace LyuOnnxCore.Calibration;

/// <summary>
/// Converts image points into motion-axis coordinates through a chessboard-defined board plane.
/// </summary>
public sealed class MotionCoordinateMapper : IDisposable
{
    private readonly BoardCoordinateMapper _boardMapper;
    private readonly Mat _machineFromBoardTransform;
    private readonly Mat _boardFromMachineTransform;
    private readonly MotionCoordinateMappingParameters _parameters;

    private MotionCoordinateMapper(
        BoardCoordinateMapper boardMapper,
        Mat machineFromBoardTransform,
        Mat boardFromMachineTransform,
        MotionCoordinateMappingParameters parameters)
    {
        _boardMapper = boardMapper;
        _machineFromBoardTransform = machineFromBoardTransform;
        _boardFromMachineTransform = boardFromMachineTransform;
        _parameters = parameters;
    }

    public MotionCoordinateMappingParameters Parameters => _parameters;

    public static MotionCoordinateMapper Create(
        BoardCoordinateMapper boardMapper,
        MotionCoordinateMappingParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(boardMapper);
        ArgumentNullException.ThrowIfNull(parameters);

        var machineFromBoardTransform = CreateMatrixMat(parameters.MachineFromBoardTransform);
        var boardFromMachineTransform = new Mat();

        if (Cv2.Invert(machineFromBoardTransform, boardFromMachineTransform) == 0)
        {
            machineFromBoardTransform.Dispose();
            boardFromMachineTransform.Dispose();
            throw new InvalidOperationException("Failed to invert the board-to-machine transform.");
        }

        return new MotionCoordinateMapper(boardMapper, machineFromBoardTransform, boardFromMachineTransform, parameters);
    }

    public static MotionCoordinateMapper CreateFromChessboard(
        Mat image,
        ChessboardCalibrationBoard board,
        MotionCoordinateMappingParameters parameters,
        CameraCalibrationResult? calibration = null,
        ChessboardDetectionOptions? detectionOptions = null,
        HomographyMethods homographyMethod = HomographyMethods.Ransac,
        double ransacReprojectionThreshold = 3.0)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(parameters);

        var boardMapper = BoardCoordinateMapper.CreateFromChessboard(
            image,
            board,
            calibration,
            detectionOptions,
            homographyMethod,
            ransacReprojectionThreshold);

        return Create(boardMapper, parameters);
    }

    public Point2d ImageToBoard(Point2d imagePoint) => _boardMapper.ImageToBoard(imagePoint);

    public Point2d[] ImageToBoard(IEnumerable<Point2d> imagePoints) => _boardMapper.ImageToBoard(imagePoints);

    public Point2d BoardToMachine(Point2d boardPoint)
    {
        return Cv2.PerspectiveTransform([boardPoint], _machineFromBoardTransform)[0];
    }

    public Point2d[] BoardToMachine(IEnumerable<Point2d> boardPoints)
    {
        ArgumentNullException.ThrowIfNull(boardPoints);
        return Cv2.PerspectiveTransform(boardPoints.ToArray(), _machineFromBoardTransform);
    }

    public Point2d MachineToBoard(Point2d machinePoint)
    {
        return Cv2.PerspectiveTransform([machinePoint], _boardFromMachineTransform)[0];
    }

    public Point2d[] MachineToBoard(IEnumerable<Point2d> machinePoints)
    {
        ArgumentNullException.ThrowIfNull(machinePoints);
        return Cv2.PerspectiveTransform(machinePoints.ToArray(), _boardFromMachineTransform);
    }

    public Point2d ImageToMachine(Point2d imagePoint)
    {
        var boardPoint = _boardMapper.ImageToBoard(imagePoint);
        return BoardToMachine(boardPoint);
    }

    public Point2d[] ImageToMachine(IEnumerable<Point2d> imagePoints)
    {
        var boardPoints = _boardMapper.ImageToBoard(imagePoints);
        return BoardToMachine(boardPoints);
    }

    public MotionPulsePoint MachineToPulse(Point2d machinePoint)
    {
        double x = _parameters.InvertPulseX ? -machinePoint.X : machinePoint.X;
        double y = _parameters.InvertPulseY ? -machinePoint.Y : machinePoint.Y;

        long pulseX = (long)Math.Round(x * _parameters.PulsePerUnitX);
        long pulseY = (long)Math.Round(y * _parameters.PulsePerUnitY);
        return new MotionPulsePoint(pulseX, pulseY);
    }

    public MotionPulsePoint[] MachineToPulse(IEnumerable<Point2d> machinePoints)
    {
        ArgumentNullException.ThrowIfNull(machinePoints);
        return machinePoints.Select(MachineToPulse).ToArray();
    }

    public MotionPulsePoint ImageToPulse(Point2d imagePoint)
    {
        var machinePoint = ImageToMachine(imagePoint);
        return MachineToPulse(machinePoint);
    }

    public MotionPulsePoint[] ImageToPulse(IEnumerable<Point2d> imagePoints)
    {
        var machinePoints = ImageToMachine(imagePoints);
        return MachineToPulse(machinePoints);
    }

    public void Dispose()
    {
        _boardMapper.Dispose();
        _machineFromBoardTransform.Dispose();
        _boardFromMachineTransform.Dispose();
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
}
