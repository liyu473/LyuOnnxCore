using LyuOnnxCore.Calibration.Models;
using OpenCvSharp;

namespace LyuOnnxCore.Calibration.Interface;

/// <summary>
/// 在九点标定的基础上，根据当前轴位置与标定时轴位置的偏移，补偿检测到的像素坐标，以提高坐标转换的准确性。
/// 水平可移动，高度不能变化的场景适用，例如：水平移动的视觉系统标定后，轴位置发生偏移时，可以使用此接口进行补偿。
/// </summary>
/// <remarks>
/// 该补偿模型基于平面映射假设：
/// 1. 九点标定建立的是同一工作平面上的像素到平台坐标映射；
/// 2. 运行时目标仍位于该工作平面上；
/// 3. currentAxisPosition、calibrationAxisZero、WorldPoint 使用同一坐标系与同一单位；
/// 4. 返回的“标定参考像素”是去畸变后的参考像素，可直接用于 PixelToWorldTransform。
/// </remarks>
public interface IAxisPositionCompensation
{
    /// <summary>
    /// 将检测到的原始像素点转换为标定参考系下的去畸变像素点。
    /// 返回值可直接用于 NinePointCalibrationResult.PixelToWorldTransform。
    /// </summary>
    Point2d TransformToCalibrationPixel(
        Point2d detectedPixel,
        Point2d currentAxisPosition,
        Point2d calibrationAxisZero,
        NinePointCalibrationResult ninePointCalibration
    );

    /// <summary>
    /// 根据当前轴偏移补偿后再转换为世界坐标。
    /// </summary>
    Point2d PixelToWorldWithCompensation(
        Point2d detectedPixel,
        Point2d currentAxisPosition,
        Point2d calibrationAxisZero,
        NinePointCalibrationResult ninePointCalibration
    );
}
