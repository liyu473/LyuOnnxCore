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

    /// <summary>
    /// 是否过滤重合框
    /// </summary>
    public bool IsFilterOverlay { get; set; } = true;

    /// <summary>
    /// 是否跨类别过滤
    /// </summary>
    public bool IsCrossClass { get; set; } = true;

    /// <summary>
    /// 重合框过滤程度 (0-1)
    /// 值越大过滤越严格，当检测框被包含的比例超过此阈值时将被过滤
    /// 推荐值: 0.8 (表示小框被大框包含80%以上时过滤)
    /// </summary>
    public float OverlayThreshold { get; set; } = 0.8f;

    /// <summary>
    /// 模型输入宽度（null 时自动从模型获取，默认 640）
    /// </summary>
    public int? InputWidth { get; set; } = null;

    /// <summary>
    /// 模型输入高度（null 时自动从模型获取，默认 640）
    /// </summary>
    public int? InputHeight { get; set; } = null;
}
