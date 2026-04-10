using OpenCvSharp;

namespace LyuOnnxCore.Calibration.Models;

public class CalibrationPair
{
    /// <summary>
    /// 图像中的像素坐标（原始图像坐标）
    /// </summary>
    public Point2d ImagePoint { get; set; }

    /// <summary>
    /// 平台坐标（单位 pulse脉冲）
    /// </summary>
    public Point2d WorldPoint { get; set; }
}
