using OpenCvSharp;
using System.Text.Json.Serialization;

namespace LyuOnnxCore.Calibration;

/// <summary>
/// Stores the planar transform from calibration-board coordinates into motion-axis coordinates.
/// </summary>
public sealed class MotionCoordinateMappingParameters
{
    public MotionCoordinateMappingParameters(
        double[,] machineFromBoardTransform,
        double pulsePerUnitX = 1.0,
        double pulsePerUnitY = 1.0,
        bool invertPulseX = false,
        bool invertPulseY = false)
    {
        ArgumentNullException.ThrowIfNull(machineFromBoardTransform);

        if (machineFromBoardTransform.GetLength(0) != 3 || machineFromBoardTransform.GetLength(1) != 3)
            throw new ArgumentException("Machine-from-board transform must be a 3x3 matrix.", nameof(machineFromBoardTransform));

        if (pulsePerUnitX <= 0)
            throw new ArgumentOutOfRangeException(nameof(pulsePerUnitX), "Pulse-per-unit scale must be greater than 0.");

        if (pulsePerUnitY <= 0)
            throw new ArgumentOutOfRangeException(nameof(pulsePerUnitY), "Pulse-per-unit scale must be greater than 0.");

        MachineFromBoardTransform = (double[,])machineFromBoardTransform.Clone();
        PulsePerUnitX = pulsePerUnitX;
        PulsePerUnitY = pulsePerUnitY;
        InvertPulseX = invertPulseX;
        InvertPulseY = invertPulseY;
    }

    [JsonConstructor]
    public MotionCoordinateMappingParameters(
        double[] machineFromBoardTransform,
        double pulsePerUnitX = 1.0,
        double pulsePerUnitY = 1.0,
        bool invertPulseX = false,
        bool invertPulseY = false)
        : this(
            ToDoubleMatrix(machineFromBoardTransform),
            pulsePerUnitX,
            pulsePerUnitY,
            invertPulseX,
            invertPulseY)
    {
    }

    [JsonIgnore]
    public double[,] MachineFromBoardTransform { get; }

    [JsonPropertyName("MachineFromBoardTransform")]
    public double[] MachineFromBoardTransformFlat => FlattenMatrix(MachineFromBoardTransform);

    /// <summary>
    /// Pulse-per-unit scaling for X. For mm-based motion coordinates this is usually pulse/mm.
    /// </summary>
    public double PulsePerUnitX { get; }

    /// <summary>
    /// Pulse-per-unit scaling for Y. For mm-based motion coordinates this is usually pulse/mm.
    /// </summary>
    public double PulsePerUnitY { get; }

    public bool InvertPulseX { get; }

    public bool InvertPulseY { get; }

    public static MotionCoordinateMappingParameters Create(
        IEnumerable<Point2d> boardPoints,
        IEnumerable<Point2d> machinePoints,
        double pulsePerUnitX = 1.0,
        double pulsePerUnitY = 1.0,
        bool invertPulseX = false,
        bool invertPulseY = false,
        HomographyMethods homographyMethod = HomographyMethods.Ransac,
        double ransacReprojectionThreshold = 3.0)
    {
        ArgumentNullException.ThrowIfNull(boardPoints);
        ArgumentNullException.ThrowIfNull(machinePoints);

        var boardPointArray = boardPoints.ToArray();
        var machinePointArray = machinePoints.ToArray();

        if (boardPointArray.Length != machinePointArray.Length)
            throw new InvalidOperationException("Board points and machine points must have the same count.");

        if (boardPointArray.Length < 4)
            throw new InvalidOperationException("At least 4 point correspondences are required to estimate the board-to-machine transform.");

        using var inlierMask = new Mat();
        using var transform = Cv2.FindHomography(
            boardPointArray,
            machinePointArray,
            homographyMethod,
            ransacReprojectionThreshold,
            inlierMask);

        if (transform.Empty())
            throw new InvalidOperationException("Failed to estimate the board-to-machine transform.");

        return new MotionCoordinateMappingParameters(
            ToDoubleMatrix(transform),
            pulsePerUnitX,
            pulsePerUnitY,
            invertPulseX,
            invertPulseY);
    }

    private static double[] FlattenMatrix(double[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        var flat = new double[rows * cols];
        int index = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < cols; column++)
            {
                flat[index++] = matrix[row, column];
            }
        }

        return flat;
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

    private static double[,] ToDoubleMatrix(IReadOnlyList<double> flatValues)
    {
        ArgumentNullException.ThrowIfNull(flatValues);

        if (flatValues.Count != 9)
            throw new ArgumentException("Machine-from-board transform must contain exactly 9 values.", nameof(flatValues));

        var matrix = new double[3, 3];
        int index = 0;
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                matrix[row, column] = flatValues[index++];
            }
        }

        return matrix;
    }
}
