using OpenCvSharp;

namespace LyuCvExCore.Models;

/// <summary>
/// 轮廓绘制选项
/// </summary>
public class ContourDrawOptions
{
    /// <summary>
    /// 是否绘制轮廓
    /// </summary>
    public bool DrawContour { get; set; } = true;

    /// <summary>
    /// 轮廓颜色
    /// </summary>
    public Scalar ContourColor { get; set; } = new Scalar(0, 255, 0);

    /// <summary>
    /// 轮廓线条粗细
    /// </summary>
    public int ContourThickness { get; set; } = 2;

    /// <summary>
    /// 是否绘制外接矩形
    /// </summary>
    public bool DrawBoundingRect { get; set; } = false;

    /// <summary>
    /// 外接矩形颜色
    /// </summary>
    public Scalar BoundingRectColor { get; set; } = new Scalar(255, 0, 0);

    /// <summary>
    /// 外接矩形线条粗细
    /// </summary>
    public int BoundingRectThickness { get; set; } = 2;

    /// <summary>
    /// 是否绘制最小外接矩形
    /// </summary>
    public bool DrawMinAreaRect { get; set; } = false;

    /// <summary>
    /// 最小外接矩形颜色
    /// </summary>
    public Scalar MinAreaRectColor { get; set; } = new Scalar(0, 0, 255);

    /// <summary>
    /// 最小外接矩形线条粗细
    /// </summary>
    public int MinAreaRectThickness { get; set; } = 2;

    /// <summary>
    /// 是否绘制质心
    /// </summary>
    public bool DrawCentroid { get; set; } = false;

    /// <summary>
    /// 质心颜色
    /// </summary>
    public Scalar CentroidColor { get; set; } = new Scalar(0, 255, 255);

    /// <summary>
    /// 是否绘制凸包
    /// </summary>
    public bool DrawConvexHull { get; set; } = false;

    /// <summary>
    /// 凸包颜色
    /// </summary>
    public Scalar ConvexHullColor { get; set; } = new Scalar(255, 255, 0);

    /// <summary>
    /// 凸包线条粗细
    /// </summary>
    public int ConvexHullThickness { get; set; } = 1;
}
