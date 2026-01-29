using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;

namespace MahTemp.Model;

/// <summary>
/// 绘制轮廓设置
/// </summary>
public partial class DrawContoursSettings : ObservableObject
{
    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = false;

    /// <summary>
    /// 轮廓线条颜色 (BGR格式)
    /// </summary>
    [ObservableProperty]
    public partial Scalar ContourColor { get; set; } = new Scalar(0, 255, 0); // 绿色

    /// <summary>
    /// 轮廓线条粗细
    /// </summary>
    [ObservableProperty]
    public partial int Thickness { get; set; } = 2;

    /// <summary>
    /// 是否绘制轮廓索引
    /// </summary>
    [ObservableProperty]
    public partial bool DrawIndex { get; set; } = false;

    /// <summary>
    /// 要绘制的轮廓索引（-1表示绘制所有轮廓）
    /// </summary>
    [ObservableProperty]
    public partial int ContourIndex { get; set; } = -1;
}
