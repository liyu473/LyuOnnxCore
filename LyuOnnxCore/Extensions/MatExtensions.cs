using OpenCvSharp;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
}
