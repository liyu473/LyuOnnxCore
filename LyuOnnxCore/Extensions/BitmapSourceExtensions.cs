using System.Windows.Media;
using System.Windows.Media.Imaging;
using LyuOnnxCore.Models;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// BitmapSource 扩展方法
/// </summary>
public static class BitmapSourceExtensions
{
    /// <summary>
    /// 将 ImageData 转换为 BitmapSource
    /// </summary>
    /// <param name="imageData">ImageData 对象</param>
    /// <returns>WPF BitmapSource 对象</returns>
    public static BitmapSource ToBitmapSource(this ImageData imageData)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        ArgumentNullException.ThrowIfNull(imageData.Data);

        var format = imageData.Channels switch
        {
            1 => PixelFormats.Gray8,
            3 => imageData.IsBgr ? PixelFormats.Bgr24 : PixelFormats.Rgb24,
            4 => imageData.IsBgr ? PixelFormats.Bgra32 : PixelFormats.Rgba64,
            _ => throw new NotSupportedException($"不支持的通道数: {imageData.Channels}")
        };

        int stride = imageData.Width * imageData.Channels;

        var bitmap = BitmapSource.Create(
            imageData.Width,
            imageData.Height,
            96,
            96,
            format,
            null,
            imageData.Data,
            stride);

        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// 将 BitmapSource 转换为 ImageData
    /// </summary>
    /// <param name="source">WPF BitmapSource 对象</param>
    /// <returns>ImageData 对象</returns>
    public static ImageData ToImageData(this BitmapSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // 确保格式为 Bgr24 或 Bgra32
        var convertedSource = source;
        int channels;
        bool isBgr = true;

        if (source.Format == PixelFormats.Bgr24)
        {
            channels = 3;
        }
        else if (source.Format == PixelFormats.Bgra32)
        {
            channels = 4;
        }
        else if (source.Format == PixelFormats.Rgb24)
        {
            channels = 3;
            isBgr = false;
        }
        else if (source.Format == PixelFormats.Gray8)
        {
            channels = 1;
            isBgr = false;
        }
        else
        {
            // 转换为 Bgr24 格式
            convertedSource = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0);
            channels = 3;
        }

        int width = convertedSource.PixelWidth;
        int height = convertedSource.PixelHeight;
        int stride = width * channels;

        byte[] pixels = new byte[height * stride];
        convertedSource.CopyPixels(pixels, stride, 0);

        return new ImageData
        {
            Width = width,
            Height = height,
            Channels = channels,
            Data = pixels,
            IsBgr = isBgr
        };
    }

    /// <summary>
    /// 将 BitmapSource 转换为 ImageData（指定目标格式）
    /// </summary>
    /// <param name="source">WPF BitmapSource 对象</param>
    /// <param name="targetFormat">目标像素格式</param>
    /// <returns>ImageData 对象</returns>
    public static ImageData ToImageData(this BitmapSource source, PixelFormat targetFormat)
    {
        ArgumentNullException.ThrowIfNull(source);

        var convertedSource = source.Format == targetFormat
            ? source
            : new FormatConvertedBitmap(source, targetFormat, null, 0);

        int channels = targetFormat.BitsPerPixel / 8;
        bool isBgr = targetFormat == PixelFormats.Bgr24 || targetFormat == PixelFormats.Bgra32;

        int width = convertedSource.PixelWidth;
        int height = convertedSource.PixelHeight;
        int stride = width * channels;

        byte[] pixels = new byte[height * stride];
        convertedSource.CopyPixels(pixels, stride, 0);

        return new ImageData
        {
            Width = width,
            Height = height,
            Channels = channels,
            Data = pixels,
            IsBgr = isBgr
        };
    }
}
