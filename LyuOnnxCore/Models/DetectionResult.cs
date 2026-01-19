namespace LyuOnnxCore.Models;

/// <summary>
/// 检测结果类，包含单个检测对象的信息
/// </summary>
public class DetectionResult
{
    /// <summary>
    /// 检测标签索引
    /// </summary>
    public int LabelIndex { get; set; }

    /// <summary>
    /// 检测标签名称
    /// </summary>
    public string LabelName { get; set; } = string.Empty;

    /// <summary>
    /// 置信度 (0-1)
    /// </summary>
    public float Confidence { get; set; }

    /// <summary>
    /// 边界框（用于标准检测）
    /// </summary>
    public BoundingBox? BoundingBox { get; set; }

    /// <summary>
    /// 旋转边界框（用于 OBB 检测）
    /// </summary>
    public OrientedBoundingBox? OrientedBoundingBox { get; set; }

    /// <summary>
    /// 是否为 OBB 检测结果
    /// </summary>
    public bool IsOBB => OrientedBoundingBox.HasValue;
}
