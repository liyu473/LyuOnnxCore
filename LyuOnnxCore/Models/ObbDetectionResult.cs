namespace LyuOnnxCore.Models;

/// <summary>
/// Oriented bounding box detection result.
/// </summary>
public sealed class ObbDetectionResult
{
    public int LabelIndex { get; init; }

    public string LabelName { get; init; } = string.Empty;

    public float Confidence { get; init; }

    public OrientedBoundingBox OrientedBoundingBox { get; init; }
}
