using LyuOnnxCore.Calibration.Models;
using OpenCvSharp;

namespace LyuOnnxCore.Calibration.Interface;

public interface ICameraCalibration
{
    CameraCalibrateResult Calibrate(
        IEnumerable<string> imagePaths,
        Size patternSize,
        float squareSizeMm
    );

    CameraCalibrateResult Calibrate(
        IEnumerable<string> imagePaths,
        Size patternSize,
        float pointSpacingMm,
        CalibrationPatternType patternType
    );

    CameraCalibrateResult Calibrate(
        IEnumerable<Mat> images,
        Size patternSize,
        float squareSizeMm
    );

    CameraCalibrateResult Calibrate(
        IEnumerable<Mat> images,
        Size patternSize,
        float pointSpacingMm,
        CalibrationPatternType patternType
    );

    string SerializeResult(
        CameraCalibrateResult result,
        bool writeIndented = true
    );

    CameraCalibrateResult DeserializeResult(string json);
}
