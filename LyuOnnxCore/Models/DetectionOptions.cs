namespace LyuOnnxCore.Models;

/// <summary>
/// 检测选项配置
/// </summary>
public class DetectionOptions
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
    /// 边界框颜色 (BGR格式)
    /// </summary>
    public (int B, int G, int R) BoxColor { get; set; } = (0, 255, 0);

    /// <summary>
    /// 边界框线条粗细
    /// </summary>
    public int BoxThickness { get; set; } = 2;

    /// <summary>
    /// 文字字体大小
    /// </summary>
    public double FontScale { get; set; } = 0.6;

    /// <summary>
    /// 文字颜色 (BGR格式)
    /// </summary>
    public (int B, int G, int R) TextColor { get; set; } = (255, 255, 255);

    /// <summary>
    /// 文字背景颜色 (BGR格式)
    /// </summary>
    public (int B, int G, int R) TextBackgroundColor { get; set; } = (0, 255, 0);

    /// <summary>
    /// 置信度阈值
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.5f;

    /// <summary>
    /// NMS (非极大值抑制) 阈值
    /// </summary>
    public float NmsThreshold { get; set; } = 0.45f;
}
