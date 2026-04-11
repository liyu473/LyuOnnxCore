using LyuOnnxCore.Calibration.Models;
using OpenCvSharp;

namespace LyuOnnxCore.Calibration.Interface;

public interface INinePointCalibration
{
    NinePointCalibrationResult Calibrate(
        IList<CalibrationPair> pairs
    );

    NinePointCalibrationResult Calibrate(
        CameraCalibrateResult cameraCalibration,
        IList<CalibrationPair> pairs
    );

    Point2d PixelToWorld(
        Point2d pixelPoint,
        NinePointCalibrationResult ninePointCalibration
    );

    Point2d PixelToWorld(
        Point2d pixelPoint,
        CameraCalibrateResult cameraCalibration,
        NinePointCalibrationResult ninePointCalibration
    );

    string SerializeResult(
        NinePointCalibrationResult result,
        bool writeIndented = true
    );

    NinePointCalibrationResult DeserializeResult(string json);
}
