namespace LyuOnnxCore.Models;

/// <summary>
/// Horizontal bounding box detection result.
/// </summary>
public sealed class HbbDetectionResult
{
    public int LabelIndex { get; init; }

    public string LabelName { get; init; } = string.Empty;

    public float Confidence { get; init; }

    public BoundingBox BoundingBox { get; init; }
}
