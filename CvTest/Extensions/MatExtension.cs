using OpenCvSharp;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LyuCvExCore.Extensions;

/// <summary>
/// Mat 扩展方法
/// </summary>
public static class MatExtension
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

    /// <summary>
    /// 将 WPF BitmapSource 转换为 OpenCV Mat
    /// </summary>
    /// <param name="source">WPF BitmapSource 对象</param>
    /// <returns>OpenCV Mat 对象</returns>
    public static Mat ToMat(this BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // 确定目标格式和通道数
        var convertedSource = source;
        MatType matType;
        PixelFormat targetFormat;

        if (source.Format == PixelFormats.Gray8)
        {
            matType = MatType.CV_8UC1;
            targetFormat = PixelFormats.Gray8;
        }
        else if (source.Format == PixelFormats.Bgr24)
        {
            matType = MatType.CV_8UC3;
            targetFormat = PixelFormats.Bgr24;
        }
        else if (source.Format == PixelFormats.Bgra32)
        {
            matType = MatType.CV_8UC4;
            targetFormat = PixelFormats.Bgra32;
        }
        else
        {
            // 默认转换为 BGR24 格式
            convertedSource = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0);
            matType = MatType.CV_8UC3;
            targetFormat = PixelFormats.Bgr24;
        }

        int width = convertedSource.PixelWidth;
        int height = convertedSource.PixelHeight;
        int channels = targetFormat.BitsPerPixel / 8;
        int stride = width * channels;

        // 复制像素数据
        byte[] pixels = new byte[height * stride];
        convertedSource.CopyPixels(pixels, stride, 0);

        // 创建 Mat 并复制数据
        var mat = new Mat(height, width, matType);
        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, mat.Data, pixels.Length);

        return mat;
    }
}

