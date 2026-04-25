namespace LyuOnnxCore.Calibration.Models;

/// <summary>
/// 相机内参标定使用的标定板类型。
/// </summary>
public enum CalibrationPatternType
{
    /// <summary>
    /// 棋盘格，patternSize 表示内角点列数和行数。
    /// </summary>
    Chessboard,

    /// <summary>
    /// 对称圆点板，patternSize 表示圆心列数和行数。
    /// </summary>
    SymmetricCirclesGrid,

    /// <summary>
    /// 非对称圆点板，patternSize 表示圆心列数和行数。
    /// </summary>
    AsymmetricCirclesGrid
}
