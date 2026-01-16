namespace LyuOnnxCore.Models;

/// <summary>
/// 绘制选项配置（与图像库无关）
/// </summary>
public class DrawOptions
{
    /// <summary>
    /// 是否显示置信度
    /// </summary>
    public bool ShowConfidence { get; set; } = true;

    /// <summary>
    /// 是否显示标签名称
    /// </summary>
    public bool ShowLabel { get; set; } = true;

    /// <summary>
    /// 边界框颜色 (B, G, R)
    /// </summary>
    public (int B, int G, int R) BoxColor { get; set; } = (0, 255, 0);

    /// <summary>
    /// 边界框线条粗细
    /// </summary>
    public int BoxThickness { get; set; } = 2;

    /// <summary>
    /// 文字字体大小
    /// </summary>
    public double FontScale { get; set; } = 0.5;

    /// <summary>
    /// 文字颜色 (B, G, R)
    /// </summary>
    public (int B, int G, int R) TextColor { get; set; } = (0, 255, 255);
}
