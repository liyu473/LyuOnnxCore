using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using LyuOnnxCore.Extensions;
using LyuOnnxCore.Models;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;
using OpenCvSharp.Extensions;

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
    /// 在图像上绘制检测结果（支持中文标签）
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
        }

        using var bitmap = BitmapConverter.ToBitmap(result);
        using var graphics = Graphics.FromImage(bitmap);
        
        // 设置文字渲染质量
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // 计算字体大小（基于 FontScale）
        float fontSize = (float)(12 * options.FontScale / 0.5);
        using var font = new Font("Microsoft YaHei", fontSize, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(options.TextColor.R, options.TextColor.G, options.TextColor.B));
        using var bgBrush = new SolidBrush(Color.FromArgb(0, 0, 0, 0)); // 半透明黑色背景

        foreach (var detection in detections)
        {
            var box = detection.BoundingBox;

            string label = "";
            if (options.ShowLabel)
                label = detection.LabelName;
            if (options.ShowConfidence)
                label += $" {detection.Confidence:P1}";

            if (!string.IsNullOrEmpty(label))
            {
                // 测量文字大小
                var textSize = graphics.MeasureString(label, font);
                
                int textX = box.X;
                int textY = box.Y - (int)textSize.Height - 2;
                if (textY < 0) textY = box.Y + 2;

                // 绘制文字背景
                graphics.FillRectangle(bgBrush, textX, textY, textSize.Width, textSize.Height);
                
                // 绘制文字
                graphics.DrawString(label, font, textBrush, textX, textY);
            }
        }

        return BitmapConverter.ToMat(bitmap);
    }
}
