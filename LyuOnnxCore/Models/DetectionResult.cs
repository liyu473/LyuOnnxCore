using OpenCvSharp;

namespace LyuOnnxCore.Models;

/// <summary>
/// 检测结果类，包含单个检测对象的信息
/// </summary>
public class DetectionResult
{
    /// <summary>
    /// 裁剪后的图像区域
    /// </summary>
    public Mat CroppedImage { get; set; } = null!;

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
    /// 边界框在原图中的坐标 (x, y, width, height)
    /// </summary>
    public Rect BoundingBox { get; set; }

    /// <summary>
    /// 边界框中心点坐标
    /// </summary>
    public Point Center => new(BoundingBox.X + BoundingBox.Width / 2, BoundingBox.Y + BoundingBox.Height / 2);

    /// <summary>
    /// 边界框面积
    /// </summary>
    public int Area => BoundingBox.Width * BoundingBox.Height;
}
