using LyuOnnxCore.Models;
using OpenCvSharp;

namespace LyuOnnxCore.Interfaces;

public interface IYoloObbDetectionService
{
    ObbDetectionResult[] Detect(
        string modelPath,
        string imagePath,
        IEnumerable<string> labels,
        DetectionOptions? detectionOptions = null
    );

    ObbDetectionResult[] Detect(
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
