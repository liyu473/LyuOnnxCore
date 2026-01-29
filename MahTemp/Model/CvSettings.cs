using CommunityToolkit.Mvvm.ComponentModel;

namespace MahTemp.Model;

/// <summary>
/// OpenCV 处理设置基类
/// </summary>
public partial class CvSettings : ObservableObject
{
    [ObservableProperty]
    public partial bool IsApplyGrayscale { get; set; } = false;

    /// <summary>
    /// 高斯模糊设置
    /// </summary>
    [ObservableProperty]
    public partial GaussianBlurSettings GaussianBlur { get; set; } = new();

    /// <summary>
    /// 二值化设置
    /// </summary>
    [ObservableProperty]
    public partial ThresholdSettings Threshold { get; set; } = new();

    /// <summary>
    /// 查找轮廓设置
    /// </summary>
    [ObservableProperty]
    public partial FindContoursSettings FindContours { get; set; } = new();

    /// <summary>
    /// 绘制轮廓设置
    /// </summary>
    [ObservableProperty]
    public partial DrawContoursSettings DrawContours { get; set; } = new();
}

