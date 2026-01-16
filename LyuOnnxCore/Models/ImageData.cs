namespace LyuOnnxCore.Models;

/// <summary>
/// 图像数据（与具体图像库解耦）
/// </summary>
public class ImageData
{
    /// <summary>
    /// 图像宽度
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图像高度
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 通道数 (1=灰度, 3=RGB, 4=RGBA)
    /// </summary>
    public int Channels { get; set; }

    /// <summary>
    /// 像素数据 (RGB/BGR 格式，按行存储)
    /// </summary>
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// 是否为 BGR 格式（OpenCV 默认）
    /// </summary>
    public bool IsBgr { get; set; } = true;
}
