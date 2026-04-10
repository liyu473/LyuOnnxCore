using System.Text.Json.Serialization;

namespace LyuOnnxCore.Calibration.Models;

public sealed class NinePointCalibrationResult
{
    [JsonIgnore]
    public double[,] PixelToWorldTransform { get; set; } = CreateIdentityMatrix();

    [JsonPropertyName("PixelToWorldTransform")]
    public double[] PixelToWorldTransformFlat
    {
        get => FlattenMatrix(PixelToWorldTransform);
        set => PixelToWorldTransform = UnflattenMatrix(value, 3, 3, nameof(PixelToWorldTransformFlat));
    }

    [JsonIgnore]
    public double[,] WorldToPixelTransform { get; set; } = CreateIdentityMatrix();

    [JsonPropertyName("WorldToPixelTransform")]
    public double[] WorldToPixelTransformFlat
    {
        get => FlattenMatrix(WorldToPixelTransform);
        set => WorldToPixelTransform = UnflattenMatrix(value, 3, 3, nameof(WorldToPixelTransformFlat));
    }

    [JsonIgnore]
    public double[,]? CameraMatrix { get; set; }

    [JsonPropertyName("CameraMatrix")]
    public double[]? CameraMatrixFlat
    {
        get => CameraMatrix is null ? null : FlattenMatrix(CameraMatrix);
        set => CameraMatrix = value is null ? null : UnflattenMatrix(value, 3, 3, nameof(CameraMatrixFlat));
    }

    public double[] DistortionCoefficients { get; set; } = [];

    public double MeanReprojectionError { get; set; }

    public double MaxReprojectionError { get; set; }

    public int PairCount { get; set; }

    public int InlierCount { get; set; }

    public CalibrationPair[] Pairs { get; set; } = [];

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
