namespace LyuOnnxCore.Models;

/// <summary>
/// ONNX 模型信息
/// </summary>
public class OnnxModelInfo
{
    /// <summary>
    /// 模型名称（不含扩展名）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 模型文件名（含扩展名）
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 模型完整路径
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 文件大小（格式化字符串）
    /// </summary>
    public string FileSizeFormatted => FormatFileSize(FileSize);

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTime LastModified { get; set; }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    public override string ToString() => $"{Name} ({FileSizeFormatted})";
}
