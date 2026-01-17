namespace LyuCvExCore.Models;

/// <summary>
/// 平滑度过滤选项
/// </summary>
public class SmoothFilterOptions
{
    /// <summary>
    /// 是否使用实心度过滤
    /// </summary>
    public bool UseSolidity { get; set; } = true;

    /// <summary>
    /// 最小实心度 (0-1)，推荐值：0.85-0.95
    /// </summary>
    public double MinSolidity { get; set; } = 0.9;

    /// <summary>
    /// 是否使用圆形度过滤
    /// </summary>
    public bool UseCircularity { get; set; } = false;

    /// <summary>
    /// 最小圆形度 (0-1)
    /// </summary>
    public double MinCircularity { get; set; } = 0.7;

    /// <summary>
    /// 是否使用凸性缺陷过滤
    /// </summary>
    public bool UseConvexityDefect { get; set; } = false;

    /// <summary>
    /// 最大凸性缺陷 (0-1)，值越小要求越平滑
    /// </summary>
    public double MaxConvexityDefect { get; set; } = 0.1;
}
