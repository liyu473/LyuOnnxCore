using OpenCvSharp;

namespace LyuCvExCore.Models;

/// <summary>
/// 轮廓检测选项
/// </summary>
public class ContourOptions
{
    /// <summary>
    /// 轮廓检索模式
    /// </summary>
    public RetrievalModes Mode { get; set; } = RetrievalModes.External;

    /// <summary>
    /// 轮廓近似方法
    /// </summary>
    public ContourApproximationModes Method { get; set; } = ContourApproximationModes.ApproxSimple;

    /// <summary>
    /// 高斯模糊核大小（0 表示不模糊）
    /// </summary>
    public int GaussianBlurSize { get; set; } = 5;

    /// <summary>
    /// 二值化类型
    /// </summary>
    public ContourThresholdType ThresholdType { get; set; } = ContourThresholdType.Otsu;

    /// <summary>
    /// 二值化阈值（用于 Binary/BinaryInv）
    /// </summary>
    public double ThresholdValue { get; set; } = 127;

    /// <summary>
    /// 自适应阈值块大小
    /// </summary>
    public int AdaptiveBlockSize { get; set; } = 11;

    /// <summary>
    /// 自适应阈值常数
    /// </summary>
    public double AdaptiveC { get; set; } = 2;

    /// <summary>
    /// Canny 边缘检测阈值1
    /// </summary>
    public double CannyThreshold1 { get; set; } = 50;

    /// <summary>
    /// Canny 边缘检测阈值2
    /// </summary>
    public double CannyThreshold2 { get; set; } = 150;

    /// <summary>
    /// 形态学操作类型（HitMiss 表示不进行形态学操作）
    /// </summary>
    public MorphTypes MorphologyOperation { get; set; } = MorphTypes.HitMiss;

    /// <summary>
    /// 形态学核形状
    /// </summary>
    public MorphShapes MorphologyShape { get; set; } = MorphShapes.Rect;

    /// <summary>
    /// 形态学核大小
    /// </summary>
    public int MorphologyKernelSize { get; set; } = 3;

    /// <summary>
    /// 形态学操作迭代次数
    /// </summary>
    public int MorphologyIterations { get; set; } = 1;

    /// <summary>
    /// 最小轮廓面积
    /// </summary>
    public double MinArea { get; set; } = 0;

    /// <summary>
    /// 最大轮廓面积
    /// </summary>
    public double MaxArea { get; set; } = double.MaxValue;

    /// <summary>
    /// 最小轮廓周长
    /// </summary>
    public double MinPerimeter { get; set; } = 0;

    /// <summary>
    /// 最大轮廓周长
    /// </summary>
    public double MaxPerimeter { get; set; } = double.MaxValue;
}
