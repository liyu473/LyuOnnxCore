using LyuOnnxCore.Extensions;
using LyuOnnxCore.Models;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace MahTemp.Helper;

/// <summary>
/// OpenCV + ONNX 检测扩展方法
/// </summary>
public static class OnnxCvExtensions
{
    /// <summary>
    /// Mat 转 ImageData
    /// </summary>
    public static ImageData ToImageData(this Mat mat)
    {
        var data = new byte[mat.Total() * mat.Channels()];
        System.Runtime.InteropServices.Marshal.Copy(mat.Data, data, 0, data.Length);

        return new ImageData
        {
            Width = mat.Width,
            Height = mat.Height,
            Channels = mat.Channels(),
            Data = data,
            IsBgr = true
        };
    }

    /// <summary>
    /// 执行检测（Mat 版本）
    /// </summary>
    public static List<DetectionResult> Detect(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? options = null)
    {
        return session.Detect(image.ToImageData(), labels, options);
    }

    /// <summary>
    /// 执行检测并绘制结果
    /// </summary>
    public static Mat DetectAndDraw(
        this InferenceSession session,
        Mat image,
        string[] labels,
        DetectionOptions? detectionOptions = null,
        DrawOptions? drawOptions = null)
    {
        var results = session.Detect(image, labels, detectionOptions);
        return image.DrawDetections(results, drawOptions);
    }

    /// <summary>
    /// 在图像上绘制检测结果
    /// </summary>
    public static Mat DrawDetections(this Mat image, List<DetectionResult> detections, DrawOptions? options = null)
    {
        options ??= new DrawOptions();
        var result = image.Clone();

        foreach (var detection in detections)
        {
            var box = detection.BoundingBox;
            var color = new Scalar(options.BoxColor.B, options.BoxColor.G, options.BoxColor.R);

            Cv2.Rectangle(result, new Rect(box.X, box.Y, box.Width, box.Height), color, options.BoxThickness);

            string label = "";
            if (options.ShowLabel)
                label = detection.LabelName;
            if (options.ShowConfidence)
                label += $" {detection.Confidence:P1}";

            if (!string.IsNullOrEmpty(label))
            {
                var textColor = new Scalar(options.TextColor.B, options.TextColor.G, options.TextColor.R);
                int textY = box.Y - 5;
                if (textY < 15) textY = box.Y + 20;

                Cv2.PutText(result, label, new Point(box.X, textY),
                    HersheyFonts.HersheySimplex, options.FontScale, textColor, 1);
            }
        }

        return result;
    }
}
