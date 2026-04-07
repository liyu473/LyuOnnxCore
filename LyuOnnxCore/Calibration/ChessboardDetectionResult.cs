using OpenCvSharp;

namespace LyuOnnxCore.Calibration;

/// <summary>
/// Holds the detected chessboard corners for one image.
/// </summary>
public sealed class ChessboardDetectionResult
{
    public ChessboardDetectionResult(bool isSuccess, Size imageSize, Point2f[] corners)
    {
        IsSuccess = isSuccess;
        ImageSize = imageSize;
        Corners = corners ?? [];
    }

    public bool IsSuccess { get; }

    public Size ImageSize { get; }

    public Point2f[] Corners { get; }
}
