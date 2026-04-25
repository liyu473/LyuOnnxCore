using System.Text.Json.Serialization;
using OpenCvSharp;

namespace LyuOnnxCore.Calibration.Models;

public sealed class CameraCalibrateResult
{
    public Size ImageSize { get; set; }

    public CalibrationPatternType PatternType { get; set; } = CalibrationPatternType.Chessboard;

    [JsonIgnore]
    public double[,] CameraMatrix { get; set; } = CreateIdentityMatrix();

    [JsonPropertyName("CameraMatrix")]
    public double[] CameraMatrixFlat
    {
        get => FlattenMatrix(CameraMatrix);
        set => CameraMatrix = UnflattenMatrix(value, 3, 3, nameof(CameraMatrixFlat));
    }

    public double[] DistortionCoefficients { get; set; } = [];

    public Vec3d[] RotationVectors { get; set; } = [];

    public Vec3d[] TranslationVectors { get; set; } = [];

    public double ReprojectionError { get; set; }

    public double[] PerViewErrors { get; set; } = [];

    public int SuccessfulImageCount { get; set; }

    public int InputImageCount { get; set; }

    public int SkippedMismatchedResolutionCount { get; set; }

    private static double[,] CreateIdentityMatrix()
    {
        var matrix = new double[3, 3];
        matrix[0, 0] = 1;
        matrix[1, 1] = 1;
        matrix[2, 2] = 1;
        return matrix;
    }

    private static double[] FlattenMatrix(double[,] matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);

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

    private static double[,] UnflattenMatrix(
        IReadOnlyList<double> flatValues,
        int rows,
        int cols,
        string paramName
    )
    {
        ArgumentNullException.ThrowIfNull(flatValues);

        if (flatValues.Count != rows * cols)
        {
            throw new ArgumentException(
                $"Matrix data for {paramName} must contain exactly {rows * cols} values.",
                paramName
            );
        }

        var matrix = new double[rows, cols];
        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < cols; column++)
            {
                matrix[row, column] = flatValues[index++];
            }
        }

        return matrix;
    }
}
