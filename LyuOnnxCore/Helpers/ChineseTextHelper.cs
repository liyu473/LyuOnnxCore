using OpenCvSharp;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace LyuOnnxCore.Helpers;

/// <summary>
/// 支持中文的文本绘制辅助类
/// </summary>
public static class ChineseTextHelper
{
    /// <summary>
    /// 在 Mat 上绘制支持中文的文本
    /// </summary>
    /// <param name="mat">目标图像</param>
    /// <param name="text">要绘制的文本</param>
    /// <param name="position">文本位置</param>
    /// <param name="fontFamily">字体名称（如 "微软雅黑", "SimHei"）</param>
    /// <param name="fontSize">字体大小</param>
    /// <param name="color">文本颜色 (BGR)</param>
    /// <param name="backgroundColor">背景颜色 (BGR)，null 表示无背景</param>
    /// <param name="thickness">文本粗细</param>
    public static void PutChineseText(
        Mat mat,
        string text,
        OpenCvSharp.Point position,
        string fontFamily = "微软雅黑",
        float fontSize = 20,
        Scalar? color = null,
        Scalar? backgroundColor = null,
        int thickness = 1)
    {
        if (string.IsNullOrEmpty(text))
            return;

        color ??= new Scalar(255, 255, 255); // 默认白色
        
        // 将 Mat 转换为 Bitmap
        using var bitmap = MatToBitmap(mat);
        using var graphics = Graphics.FromImage(bitmap);
        
        // 设置高质量渲染
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

        // 创建字体
        using var font = new Font(fontFamily, fontSize, thickness > 1 ? FontStyle.Bold : FontStyle.Regular);
        
        // 测量文本大小
        var textSize = graphics.MeasureString(text, font);
        
        // 绘制背景
        if (backgroundColor.HasValue)
        {
            var bgColor = System.Drawing.Color.FromArgb(
                (int)backgroundColor.Value.Val2,  // B
                (int)backgroundColor.Value.Val1,  // G
                (int)backgroundColor.Value.Val0); // R
            
            using var bgBrush = new SolidBrush(bgColor);
            graphics.FillRectangle(bgBrush, position.X, position.Y - textSize.Height, 
                textSize.Width + 4, textSize.Height + 4);
        }

        // 绘制文本
        var textColor = System.Drawing.Color.FromArgb(
            (int)color.Value.Val2,  // B
            (int)color.Value.Val1,  // G
            (int)color.Value.Val0); // R
        
        using var brush = new SolidBrush(textColor);
        graphics.DrawString(text, font, brush, position.X + 2, position.Y - textSize.Height + 2);

        // 将 Bitmap 转换回 Mat
        BitmapToMat(bitmap, mat);
    }

    /// <summary>
    /// 测量中文文本的大小
    /// </summary>
    public static (int Width, int Height) MeasureChineseText(
        string text,
        string fontFamily = "微软雅黑",
        float fontSize = 20,
        int thickness = 1)
    {
        if (string.IsNullOrEmpty(text))
            return (0, 0);

        using var bitmap = new Bitmap(1, 1);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font(fontFamily, fontSize, thickness > 1 ? FontStyle.Bold : FontStyle.Regular);
        
        var size = graphics.MeasureString(text, font);
        return ((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height));
    }

    /// <summary>
    /// 将 Mat 转换为 Bitmap
    /// </summary>
    private static Bitmap MatToBitmap(Mat mat)
    {
        var bitmap = new Bitmap(mat.Width, mat.Height, PixelFormat.Format24bppRgb);
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

        unsafe
        {
            byte* srcPtr = (byte*)mat.DataPointer;
            byte* dstPtr = (byte*)bitmapData.Scan0;
            int stride = bitmapData.Stride;
            int matStride = (int)mat.Step();

            for (int y = 0; y < mat.Height; y++)
            {
                byte* srcRow = srcPtr + y * matStride;
                byte* dstRow = dstPtr + y * stride;
                
                for (int x = 0; x < mat.Width; x++)
                {
                    int srcIdx = x * 3;
                    int dstIdx = x * 3;
                    
                    // BGR to BGR (same order)
                    dstRow[dstIdx + 0] = srcRow[srcIdx + 0]; // B
                    dstRow[dstIdx + 1] = srcRow[srcIdx + 1]; // G
                    dstRow[dstIdx + 2] = srcRow[srcIdx + 2]; // R
                }
            }
        }

        bitmap.UnlockBits(bitmapData);
        return bitmap;
    }

    /// <summary>
    /// 将 Bitmap 转换回 Mat
    /// </summary>
    private static void BitmapToMat(Bitmap bitmap, Mat mat)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        unsafe
        {
            byte* srcPtr = (byte*)bitmapData.Scan0;
            byte* dstPtr = (byte*)mat.DataPointer;
            int stride = bitmapData.Stride;
            int matStride = (int)mat.Step();

            for (int y = 0; y < mat.Height; y++)
            {
                byte* srcRow = srcPtr + y * stride;
                byte* dstRow = dstPtr + y * matStride;
                
                for (int x = 0; x < mat.Width; x++)
                {
                    int srcIdx = x * 3;
                    int dstIdx = x * 3;
                    
                    // BGR to BGR (same order)
                    dstRow[dstIdx + 0] = srcRow[srcIdx + 0]; // B
                    dstRow[dstIdx + 1] = srcRow[srcIdx + 1]; // G
                    dstRow[dstIdx + 2] = srcRow[srcIdx + 2]; // R
                }
            }
        }

        bitmap.UnlockBits(bitmapData);
    }
}
