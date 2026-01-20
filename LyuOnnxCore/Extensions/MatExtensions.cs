using OpenCvSharp;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LyuOnnxCore.Models;
using System.IO;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// OpenCV Mat 扩展方法
/// </summary>
public static class MatExtensions
{
    /// <summary>
    /// 将 Mat 转换为 WPF BitmapSource
    /// </summary>
    /// <param name="mat">OpenCV Mat 对象</param>
    /// <returns>WPF BitmapSource 对象</returns>
    public static BitmapSource ToBitmapSource(this Mat mat)
    {
        if (mat == null || mat.Empty())
            throw new ArgumentException("Mat 不能为空", nameof(mat));
        int channels = mat.Channels();

        // 确定像素格式
        var pixelFormat = channels switch
        {
            1 => PixelFormats.Gray8,
            3 => PixelFormats.Bgr24,
            4 => PixelFormats.Bgra32,
            _ => throw new NotSupportedException($"不支持的通道数: {channels}"),
        };

        // 计算步长
        int stride = mat.Width * channels;

        // 复制数据到字节数组
        byte[] buffer = new byte[mat.Height * stride];
        System.Runtime.InteropServices.Marshal.Copy(mat.Data, buffer, 0, buffer.Length);

        // 创建 BitmapSource
        var bitmap = BitmapSource.Create(
            mat.Width,
            mat.Height,
            96,
            96,
            pixelFormat,
            null,
            buffer,
            stride);

        // 冻结以提高性能并允许跨线程访问
        bitmap.Freeze();

        return bitmap;
    }

    /// <summary>
    /// 将 BitmapSource 转换为 Mat
    /// </summary>
    /// <param name="source">WPF BitmapSource 对象</param>
    /// <returns>OpenCV Mat 对象</returns>
    public static Mat ToMat(this BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // 转换为 Bgr24 格式
        var convertedSource = source;
        if (source.Format != PixelFormats.Bgr24 && source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Gray8)
        {
            convertedSource = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0);
        }

        int width = convertedSource.PixelWidth;
        int height = convertedSource.PixelHeight;
        int channels = convertedSource.Format.BitsPerPixel / 8;
        int stride = width * channels;

        // 复制像素数据
        byte[] pixels = new byte[height * stride];
        convertedSource.CopyPixels(pixels, stride, 0);

        // 创建 Mat
        MatType matType = channels switch
        {
            1 => MatType.CV_8UC1,
            3 => MatType.CV_8UC3,
            4 => MatType.CV_8UC4,
            _ => throw new NotSupportedException($"不支持的通道数: {channels}")
        };

        var mat = new Mat(height, width, matType);
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, mat.Data, pixels.Length);

        return mat;
    }

    /// <summary>
    /// 裁剪并保存检测区域到指定文件夹
    /// </summary>
    /// <param name="image">原始图像</param>
    /// <param name="detections">检测结果列表</param>
    /// <param name="outputFolder">输出文件夹路径</param>
    /// <param name="errorMessages">输出参数，返回错误信息列表</param>
    /// <param name="fileNamePrefix">文件名前缀，默认为 "crop"</param>
    /// <returns>保存的文件路径列表</returns>
    public static List<string> SaveCroppedRegions(
        this Mat image,
        List<DetectionResult> detections,
        string outputFolder,
        out List<string> errorMessages,
        string fileNamePrefix = "crop")
    {
        errorMessages = [];
        
        if (image == null || image.Empty())
            throw new ArgumentException("图像不能为空", nameof(image));

        if (detections == null || detections.Count == 0)
            return [];

        // 确保输出文件夹存在
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
        }

        var savedFiles = new List<string>();
        int index = 0;

        foreach (var detection in detections)
        {
            try
            {
                Mat? croppedMat = null;

                // 处理标准边界框
                if (detection.BoundingBox.HasValue)
                {
                    var box = detection.BoundingBox.Value;
                    
                    // 计算边界框与图像的交集区域
                    int x1 = Math.Max(0, box.X);
                    int y1 = Math.Max(0, box.Y);
                    int x2 = Math.Min(image.Width, box.X + box.Width);
                    int y2 = Math.Min(image.Height, box.Y + box.Height);
                    
                    int width = x2 - x1;
                    int height = y2 - y1;

                    if (width > 0 && height > 0)
                    {
                        var rect = new Rect(x1, y1, width, height);
                        croppedMat = new Mat(image, rect);
                    }
                    else
                    {
                        errorMessages.Add($"索引 {index} ({detection.LabelName}): 边界框无效 (width={width}, height={height})");
                    }
                }
                else if (detection.OrientedBoundingBox.HasValue)
                {
                    var obb = detection.OrientedBoundingBox.Value;
                    croppedMat = CropRotatedRect(image, obb, out string debugInfo);
                    if (croppedMat == null)
                    {
                        errorMessages.Add($"索引 {index} ({detection.LabelName}): OBB 裁剪失败 - {debugInfo}");
                    }
                }

                if (croppedMat != null && !croppedMat.Empty())
                {
                    // 生成文件名：前缀_标签_索引_置信度.jpg
                    string fileName = $"{fileNamePrefix}_{detection.LabelName}_{index}_{detection.Confidence:F2}.jpg";
                    string filePath = Path.Combine(outputFolder, fileName);

                    // 保存裁剪的图像
                    Cv2.ImWrite(filePath, croppedMat);
                    savedFiles.Add(filePath);

                    croppedMat.Dispose();
                }
                else if (croppedMat == null)
                {
                    errorMessages.Add($"索引 {index} ({detection.LabelName}): 裁剪结果为空");
                }

                index++;
            }
            catch (Exception ex)
            {
                // 记录错误但继续处理其他检测结果
                errorMessages.Add($"索引 {index} ({detection.LabelName}): {ex.Message}");
            }
        }

        return savedFiles;
    }

    /// <summary>
    /// 裁剪并保存单个检测区域
    /// </summary>
    /// <param name="image">原始图像</param>
    /// <param name="detection">检测结果</param>
    /// <param name="filePath">输出文件路径</param>
    /// <returns>是否保存成功</returns>
    public static bool SaveCroppedRegion(
        this Mat image,
        DetectionResult detection,
        string filePath)
    {
        if (image == null || image.Empty())
            throw new ArgumentException("图像不能为空", nameof(image));

        if (detection == null)
            return false;

        try
        {
            Mat? croppedMat = null;

            // 处理标准边界框
            if (detection.BoundingBox.HasValue)
            {
                var box = detection.BoundingBox.Value;
                
                // 计算边界框与图像的交集区域
                int x1 = Math.Max(0, box.X);
                int y1 = Math.Max(0, box.Y);
                int x2 = Math.Min(image.Width, box.X + box.Width);
                int y2 = Math.Min(image.Height, box.Y + box.Height);
                
                int width = x2 - x1;
                int height = y2 - y1;

                if (width > 0 && height > 0)
                {
                    var rect = new Rect(x1, y1, width, height);
                    croppedMat = new Mat(image, rect);
                }
            }
            // 处理旋转边界框 (OBB)
            else if (detection.OrientedBoundingBox.HasValue)
            {
                croppedMat = CropRotatedRect(image, detection.OrientedBoundingBox.Value, out _);
            }

            if (croppedMat != null)
            {
                // 确保输出文件夹存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 保存裁剪的图像
                Cv2.ImWrite(filePath, croppedMat);
                croppedMat.Dispose();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存裁剪区域失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 裁剪旋转矩形区域
    /// </summary>
    /// <param name="image">原始图像</param>
    /// <param name="obb">旋转边界框</param>
    /// <param name="debugInfo">调试信息</param>
    /// <returns>裁剪并旋转校正后的图像</returns>
    private static Mat? CropRotatedRect(Mat image, OrientedBoundingBox obb, out string debugInfo)
    {
        debugInfo = "";
        try
        {
            // 使用角点计算实际尺寸（与绘制方法一致）
            var corners = obb.GetCornerPoints();
            
            // 计算实际宽高（从角点）
            float actualWidth = MathF.Sqrt(
                MathF.Pow(corners[1].X - corners[0].X, 2) + 
                MathF.Pow(corners[1].Y - corners[0].Y, 2));
            float actualHeight = MathF.Sqrt(
                MathF.Pow(corners[3].X - corners[0].X, 2) + 
                MathF.Pow(corners[3].Y - corners[0].Y, 2));

            // 验证尺寸
            if (actualWidth <= 0 || actualHeight <= 0)
            {
                debugInfo = $"OBB尺寸无效: W={actualWidth:F2}, H={actualHeight:F2}";
                return null;
            }

            int outputWidth = (int)Math.Round(actualWidth);
            int outputHeight = (int)Math.Round(actualHeight);
            
            debugInfo = $"OBB: Corners=[({corners[0].X:F1},{corners[0].Y:F1}),({corners[1].X:F1},{corners[1].Y:F1}),({corners[2].X:F1},{corners[2].Y:F1}),({corners[3].X:F1},{corners[3].Y:F1})], Size={outputWidth}x{outputHeight}";

            // 源点（OBB 的四个角点）
            var srcPoints = new Point2f[]
            {
                new(corners[0].X, corners[0].Y),  // 左上
                new(corners[1].X, corners[1].Y),  // 右上
                new(corners[2].X, corners[2].Y),  // 右下
                new(corners[3].X, corners[3].Y)   // 左下
            };

            // 目标点（输出矩形的四个角点）
            var dstPoints = new Point2f[]
            {
                new(0, 0),                              // 左上
                new(outputWidth - 1, 0),                // 右上
                new(outputWidth - 1, outputHeight - 1), // 右下
                new(0, outputHeight - 1)                // 左下
            };

            // 获取透视变换矩阵
            using var transformMatrix = Cv2.GetPerspectiveTransform(srcPoints, dstPoints);

            // 执行透视变换
            var result = new Mat();
            Cv2.WarpPerspective(image, result, transformMatrix, new Size(outputWidth, outputHeight),
                InterpolationFlags.Linear, BorderTypes.Constant, new Scalar(0, 0, 0));

            if (result.Empty())
            {
                debugInfo += " - 透视变换后图像为空";
                return null;
            }

            debugInfo += $", Result={result.Width}x{result.Height} - 成功";
            return result;
        }
        catch (Exception ex)
        {
            debugInfo += $" - 异常: {ex.Message}";
            return null;
        }
    }
}
