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
    /// 二值化阈值
    /// </summary>
    [ObservableProperty]
    public partial double ThresholdValue { get; set; } = 127;

    /// <summary>
    /// 二值化最大值
    /// </summary>
    [ObservableProperty]
    public partial double MaxValue { get; set; } = 255;

    /// <summary>
    /// 二值化类型
    /// </summary>
    [ObservableProperty]
    public partial ThresholdTypes Type { get; set; } = ThresholdTypes.Binary;
}
