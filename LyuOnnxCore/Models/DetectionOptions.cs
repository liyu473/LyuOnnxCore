namespace LyuOnnxCore.Models;

/// <summary>
/// 检测选项配置
/// </summary>
public class DetectionOptions
{
    /// <summary>
    /// 置信度阈值
    /// </summary>
    public float ConfidenceThreshold { get; set; } = 0.25f;

    /// <summary>
    /// NMS (非极大值抑制) 阈值
    /// </summary>
    public float NmsThreshold { get; set; } = 0.45f;

    /// <summary>
    /// 要过滤的标签名称列表，为空或null时返回所有检测结果
    /// </summary>
    public string[]? FilterLabels { get; set; }
}
