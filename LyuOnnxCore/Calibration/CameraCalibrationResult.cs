using OpenCvSharp;
using System.Text.Json.Serialization;

namespace LyuOnnxCore.Calibration;

/// <summary>
/// Result of camera intrinsic calibration.
/// </summary>
public sealed class CameraCalibrationResult
{
    public CameraCalibrationResult(
        Size imageSize,
        double[,] cameraMatrix,
        double[] distortionCoefficients,
        Vec3d[] rotationVectors,
        Vec3d[] translationVectors,
        double reprojectionError,
        double[] perViewErrors,
        int successfulImageCount,
        int inputImageCount,
        int skippedMismatchedResolutionCount)
    {
        ImageSize = imageSize;
        CameraMatrix = cameraMatrix ?? throw new ArgumentNullException(nameof(cameraMatrix));
        DistortionCoefficients = distortionCoefficients ?? throw new ArgumentNullException(nameof(distortionCoefficients));
        RotationVectors = rotationVectors ?? throw new ArgumentNullException(nameof(rotationVectors));
        TranslationVectors = translationVectors ?? throw new ArgumentNullException(nameof(translationVectors));
        ReprojectionError = reprojectionError;
        PerViewErrors = perViewErrors ?? throw new ArgumentNullException(nameof(perViewErrors));
        SuccessfulImageCount = successfulImageCount;
        InputImageCount = inputImageCount;
        SkippedMismatchedResolutionCount = skippedMismatchedResolutionCount;
    }

    public Size ImageSize { get; }

    [JsonIgnore]
    public double[,] CameraMatrix { get; }

    [JsonPropertyName("CameraMatrix")]
    public double[] CameraMatrixFlat => FlattenMatrix(CameraMatrix);

    public double[] DistortionCoefficients { get; }

    public Vec3d[] RotationVectors { get; }

    public Vec3d[] TranslationVectors { get; }

    public double ReprojectionError { get; }

    public double[] PerViewErrors { get; }

    public int SuccessfulImageCount { get; }

    public int InputImageCount { get; }

    public int SkippedMismatchedResolutionCount { get; }

    private static double[] FlattenMatrix(double[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int cols = matrix.GetLength(1);
        var flat = new double[rows * cols];
        int index = 0;
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                flat[index++] = matrix[i, j];
            }
        }
        return flat;
    }
}
