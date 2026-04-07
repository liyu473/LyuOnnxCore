namespace LyuOnnxCore.Calibration;

/// <summary>
/// Represents a 2-axis motion controller target in pulse units.
/// </summary>
public readonly record struct MotionPulsePoint(long X, long Y);
