using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;

namespace MahTemp.Model;

/// <summary>
/// 二值化设置
/// </summary>
public partial class ThresholdSettings : ObservableObject
{
    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = false;

    /// <summary>
    /// 是否使用自适应二值化
    /// </summary>
    [ObservableProperty]
    public partial bool UseAdaptive { get; set; } = false;

    /// <summary>
    /// 二值化阈值（普通二值化使用）
    /// </summary>
    [ObservableProperty]
    public partial double ThresholdValue { get; set; } = 127;

    /// <summary>
    /// 二值化最大值
    /// </summary>
    [ObservableProperty]
    public partial double MaxValue { get; set; } = 255;

    /// <summary>
    /// 二值化类型（普通二值化使用）
    /// </summary>
    [ObservableProperty]
    public partial ThresholdTypes Type { get; set; } = ThresholdTypes.Binary;

    #region 自适应二值化参数

    /// <summary>
    /// 自适应方法类型
    /// </summary>
    [ObservableProperty]
    public partial AdaptiveThresholdTypes AdaptiveMethod { get; set; } = AdaptiveThresholdTypes.GaussianC;

    /// <summary>
    /// 自适应二值化类型（只能是 Binary 或 BinaryInv）
    /// </summary>
    [ObservableProperty]
    public partial ThresholdTypes AdaptiveType { get; set; } = ThresholdTypes.Binary;

    /// <summary>
    /// 邻域块大小（必须为奇数，如 3, 5, 7...）
    /// </summary>
    [ObservableProperty]
    public partial int BlockSize { get; set; } = 41;

    /// <summary>
    /// 常数C（从平均值或加权平均值中减去的常数）
    /// </summary>
    [ObservableProperty]
    public partial double C { get; set; } = 3;

    #endregion
}

