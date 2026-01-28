using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;

namespace MahTemp.Model;

public partial class CvSettings : ObservableObject
{
    /// <summary>
    /// 处理类型
    /// </summary>
    [ObservableProperty]
    public partial CvProcessType ProcessType { get; set; } = CvProcessType.None;

    #region 轮廓检测相关参数

    /// <summary>
    /// 灰度转换方法
    /// </summary>
    [ObservableProperty]
    public partial GrayConversionMethod GrayMethod { get; set; } = GrayConversionMethod.BGR2GRAY;

    /// <summary>
    /// 是否在检测前进行高斯模糊
    /// </summary>
    [ObservableProperty]
    public partial bool ApplyGaussianBlur { get; set; } = true;

    /// <summary>
    /// 高斯模糊核大小
    /// </summary>
    [ObservableProperty]
    public partial int GaussianBlurKernelSize { get; set; } = 5;

    /// <summary>
    /// 二值化阈值
    /// </summary>
    [ObservableProperty]
    public partial double ThresholdValue { get; set; } = 127;

    /// <summary>
    /// 二值化最大值
    /// </summary>
    [ObservableProperty]
    public partial double ThresholdMaxValue { get; set; } = 255;

    /// <summary>
    /// 二值化类型
    /// </summary>
    [ObservableProperty]
    public partial ThresholdTypes ThresholdType { get; set; } = ThresholdTypes.Binary;

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
    /// 轮廓线条颜色 (BGR格式)
    /// </summary>
    [ObservableProperty]
    public partial Scalar ContourColor { get; set; } = new Scalar(0, 255, 0); // 绿色

    /// <summary>
    /// 轮廓线条粗细
    /// </summary>
    [ObservableProperty]
    public partial int ContourThickness { get; set; } = 2;

    /// <summary>
    /// 最小轮廓面积（过滤小轮廓）
    /// </summary>
    [ObservableProperty]
    public partial double MinContourArea { get; set; } = 100;

    /// <summary>
    /// 是否绘制轮廓索引
    /// </summary>
    [ObservableProperty]
    public partial bool DrawContourIndex { get; set; } = false;

    #endregion
}

/// <summary>
/// OpenCV 处理类型枚举
/// </summary>
public enum CvProcessType
{
    None,           // 无处理
    ContourDetection, // 轮廓检测
    EdgeDetection,    // 边缘检测
    ColorFilter,      // 颜色过滤
    Morphology,       // 形态学操作
}

/// <summary>
/// 灰度转换方法枚举
/// </summary>
public enum GrayConversionMethod
{
    /// <summary>
    /// 标准灰度转换 (0.299*R + 0.587*G + 0.114*B)
    /// 最常用，符合人眼感知
    /// </summary>
    BGR2GRAY,

    /// <summary>
    /// 仅使用蓝色通道
    /// 适合蓝色信息重要的场景
    /// </summary>
    BlueChannel,

    /// <summary>
    /// 仅使用绿色通道
    /// 适合植物、自然场景
    /// </summary>
    GreenChannel,

    /// <summary>
    /// 仅使用红色通道
    /// 适合红色信息重要的场景
    /// </summary>
    RedChannel,

    /// <summary>
    /// 平均值法 (R+G+B)/3
    /// 简单快速，但不符合人眼感知
    /// </summary>
    Average,

    /// <summary>
    /// 最大值法 max(R,G,B)
    /// 保留最亮的通道
    /// </summary>
    MaxValue,

    /// <summary>
    /// 最小值法 min(R,G,B)
    /// 保留最暗的通道
    /// </summary>
    MinValue
}
