using CommunityToolkit.Mvvm.ComponentModel;

namespace MahTemp.Model;

/// <summary>
/// 高斯模糊设置
/// </summary>
public partial class GaussianBlurSettings : ObservableObject
{
    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = false;

    /// <summary>
    /// 高斯模糊核大小（必须为奇数）
    /// </summary>
    [ObservableProperty]
    public partial int KernelSize { get; set; } = 5;

    /// <summary>
    /// X方向的标准差（0表示自动计算）
    /// </summary>
    [ObservableProperty]
    public partial double SigmaX { get; set; } = 0;

    /// <summary>
    /// Y方向的标准差（0表示自动计算）
    /// </summary>
    [ObservableProperty]
    public partial double SigmaY { get; set; } = 0;
}
