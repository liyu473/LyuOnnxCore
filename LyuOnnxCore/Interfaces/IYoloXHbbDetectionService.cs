using LyuOnnxCore.Models;
using OpenCvSharp;

namespace LyuOnnxCore.Interfaces;

public interface IYoloXHbbDetectionService
{
    HbbDetectionResult[] Detect(
        string modelPath,
        string imagePath,
        IEnumerable<string> labels,
        DetectionOptions? detectionOptions = null
    );

    HbbDetectionResult[] Detect(
        string modelPath,
        Mat image,
        IEnumerable<string> labels,
        DetectionOptions? detectionOptions = null
    );

    Mat DetectAndVisualize(
        string modelPath,
        string imagePath,
        IEnumerable<string> labels,
        DetectionOptions? detectionOptions = null,
        DrawOptions? drawOptions = null
    );

    Mat DetectAndVisualize(
        string modelPath,
        Mat image,
        IEnumerable<string> labels,
        DetectionOptions? detectionOptions = null,
        DrawOptions? drawOptions = null
    );
}
