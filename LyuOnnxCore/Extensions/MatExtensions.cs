using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// Mat 扩展方法
/// </summary>
public static class MatExtensions
{
    /// <summary>
    /// 将 Mat 转换为 WPF BitmapSource
    /// </summary>
    /// <param name="mat">OpenCV Mat 对象</param>
    /// <returns>WPF BitmapSource</returns>
    public static BitmapSource ToBitmapSource(this Mat mat)
    {
        if (mat == null || mat.IsDisposed)
            throw new ArgumentNullException(nameof(mat));

        var format = mat.Channels() switch
        {
            1 => PixelFormats.Gray8,
            3 => PixelFormats.Bgr24,
            4 => PixelFormats.Bgra32,
            _ => throw new NotSupportedException($"不支持的通道数: {mat.Channels()}")
        };

        var width = mat.Width;
        var height = mat.Height;
        var stride = (int)mat.Step();

        var bitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            format,
            null,
            mat.Data,
            height * stride,
            stride);

        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// 将 Mat 转换为字节数组
    /// </summary>
    /// <param name="mat">OpenCV Mat 对象</param>
    /// <param name="extension">图像格式扩展名（如 ".png", ".jpg"）</param>
    /// <returns>图像字节数组</returns>
    public static byte[] ToBytes(this Mat mat, string extension = ".png")
    {
        return Cv2.ImEncode(extension, mat, out var buf) ? buf : [];
    }
}
