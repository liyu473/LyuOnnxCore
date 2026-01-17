namespace LyuCvExCore.Models;

/// <summary>
/// 轮廓二值化类型
/// </summary>
public enum ContourThresholdType
{
    /// <summary>
    /// 固定阈值二值化
    /// </summary>
    Binary,

    /// <summary>
    /// 固定阈值反向二值化
    /// </summary>
    BinaryInv,

    /// <summary>
    /// Otsu 自动阈值
    /// </summary>
    Otsu,

    /// <summary>
    /// Otsu 自动阈值（反向）
    /// </summary>
    OtsuInv,

    /// <summary>
    /// 自适应阈值
    /// </summary>
    Adaptive,

    /// <summary>
    /// 自适应阈值（反向）
    /// </summary>
    AdaptiveInv,

    /// <summary>
    /// Canny 边缘检测
    /// </summary>
    Canny
}
