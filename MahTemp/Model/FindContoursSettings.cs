using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;

namespace MahTemp.Model;

/// <summary>
/// 查找轮廓设置
/// </summary>
public partial class FindContoursSettings : ObservableObject
{
    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = false;

    /// <summary>
    /// 轮廓检索模式
    /// </summary>
    [ObservableProperty]
    public partial RetrievalModes RetrievalMode { get; set; } = RetrievalModes.External;

    /// <summary>
    /// 轮廓近似方法
    /// </summary>
    [ObservableProperty]
    public partial ContourApproximationModes ApproximationMode { get; set; } = ContourApproximationModes.ApproxSimple;

    /// <summary>
    /// 最小轮廓面积（过滤小轮廓）
    /// </summary>
    [ObservableProperty]
    public partial double MinContourArea { get; set; } = 100;
}
