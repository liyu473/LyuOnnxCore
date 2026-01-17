using OpenCvSharp;

namespace LyuCvExCore.Models;

/// <summary>
/// 轮廓信息
/// </summary>
public class ContourInfo
{
    /// <summary>
    /// 轮廓点
    /// </summary>
    public Point[] Points { get; set; } = [];

    /// <summary>
    /// 轮廓面积
    /// </summary>
    public double Area { get; set; }

    /// <summary>
    /// 轮廓周长
    /// </summary>
    public double Perimeter { get; set; }

    /// <summary>
    /// 外接矩形
    /// </summary>
    public Rect BoundingRect { get; set; }

    /// <summary>
    /// 最小外接旋转矩形
    /// </summary>
    public RotatedRect MinAreaRect { get; set; }

    /// <summary>
    /// 质心
    /// </summary>
    public Point2f Centroid { get; set; }

    /// <summary>
    /// 圆形度 (0-1)，1 表示完美圆形
    /// </summary>
    public double Circularity { get; set; }

    /// <summary>
    /// 实心度 (0-1)，轮廓面积与凸包面积的比值
    /// </summary>
    public double Solidity { get; set; }

    /// <summary>
    /// 凸包点
    /// </summary>
    public Point[] ConvexHull { get; set; } = [];

    /// <summary>
    /// 层级索引
    /// </summary>
    public int HierarchyIndex { get; set; }
}
