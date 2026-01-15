using System.IO;
using LyuOnnxCore.Models;

namespace LyuOnnxCore.Helpers;

/// <summary>
/// ONNX 模型帮助类
/// </summary>
public static class OnnxModelHelper
{
    /// <summary>
    /// 获取指定文件夹内的所有 ONNX 模型列表
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="searchOption">搜索选项（是否包含子文件夹）</param>
    /// <returns>ONNX 模型信息列表</returns>
    public static List<OnnxModelInfo> GetOnnxModels(string folderPath, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("文件夹路径不能为空", nameof(folderPath));

        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"文件夹不存在: {folderPath}");

        var models = new List<OnnxModelInfo>();

        try
        {
            var onnxFiles = Directory.GetFiles(folderPath, "*.onnx", searchOption);

            foreach (var filePath in onnxFiles)
            {
                var fileInfo = new FileInfo(filePath);
                
                models.Add(new OnnxModelInfo
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    FileName = fileInfo.Name,
                    FullPath = fileInfo.FullName,
                    FileSize = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime
                });
            }

            // 按名称排序
            models = [.. models.OrderBy(m => m.Name)];
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException($"没有权限访问文件夹: {folderPath}", ex);
        }

        return models;
    }

    /// <summary>
    /// 获取指定文件夹内的所有 ONNX 模型列表（异步）
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="searchOption">搜索选项（是否包含子文件夹）</param>
    /// <returns>ONNX 模型信息列表</returns>
    public static async Task<List<OnnxModelInfo>> GetOnnxModelsAsync(string folderPath, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        return await Task.Run(() => GetOnnxModels(folderPath, searchOption));
    }

    /// <summary>
    /// 检查指定路径是否为有效的 ONNX 模型文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否为有效的 ONNX 文件</returns>
    public static bool IsValidOnnxFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        if (!File.Exists(filePath))
            return false;

        return Path.GetExtension(filePath).Equals(".onnx", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 根据名称查找 ONNX 模型
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="modelName">模型名称（不含扩展名）</param>
    /// <param name="searchOption">搜索选项</param>
    /// <returns>找到的模型信息，未找到返回 null</returns>
    public static OnnxModelInfo? FindModelByName(string folderPath, string modelName, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var models = GetOnnxModels(folderPath, searchOption);
        return models.FirstOrDefault(m => m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取文件夹内 ONNX 模型的总数
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="searchOption">搜索选项</param>
    /// <returns>模型数量</returns>
    public static int GetModelCount(string folderPath, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!Directory.Exists(folderPath))
            return 0;

        try
        {
            return Directory.GetFiles(folderPath, "*.onnx", searchOption).Length;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 获取文件夹内所有 ONNX 模型的总大小
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <param name="searchOption">搜索选项</param>
    /// <returns>总大小（字节）</returns>
    public static long GetTotalModelSize(string folderPath, SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var models = GetOnnxModels(folderPath, searchOption);
        return models.Sum(m => m.FileSize);
    }
}
