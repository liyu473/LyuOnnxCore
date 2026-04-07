using OpenCvSharp;

namespace LyuOnnxCore.Calibration;

/// <summary>
/// Controls chessboard corner detection behavior.
/// </summary>
public sealed class ChessboardDetectionOptions
{
    public bool UseSectorBasedDetector { get; init; } = true;

    public bool RefineCornersWithSubPixel { get; init; } = true;

    public bool ConvertToGrayFirst { get; init; } = true;

    public ChessboardFlags Flags { get; init; } =
        ChessboardFlags.AdaptiveThresh
        | ChessboardFlags.NormalizeImage
        | ChessboardFlags.Exhaustive;

    public Size SubPixelWindowSize { get; init; } = new(11, 11);

    public Size SubPixelZeroZone { get; init; } = new(-1, -1);

    public TermCriteria SubPixelCriteria { get; init; } =
        new(CriteriaTypes.Eps | CriteriaTypes.MaxIter, 30, 0.001);
}
