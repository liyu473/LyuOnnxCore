using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
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
                    LastModified = fileInfo.LastWriteTime,
                    Labels = GetModelLabels(filePath)
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
    /// 从 ONNX 元数据或同目录标签文件读取模型类别名称。
    /// </summary>
    public static string[] GetModelLabels(string modelPath)
    {
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            return [];

        try
        {
            using var session = new InferenceSession(modelPath);
            if (session.ModelMetadata.CustomMetadataMap.TryGetValue("names", out var names))
            {
                var metadataLabels = ParseLabels(names);
                if (metadataLabels.Length > 0)
                    return metadataLabels;
            }
        }
        catch
        {
        }

        var directory = Path.GetDirectoryName(modelPath) ?? string.Empty;
        var sidecarPaths = new[]
        {
            Path.ChangeExtension(modelPath, ".txt"),
            Path.Combine(directory, "classes.txt")
        };

        foreach (var sidecarPath in sidecarPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(sidecarPath))
                continue;

            var labels = File.ReadLines(sidecarPath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            if (labels.Length > 0)
                return labels;
        }

        return [];
    }

    private static string[] ParseLabels(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var labels = document.RootElement.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
                    .ToArray();
                return labels.All(item => !string.IsNullOrWhiteSpace(item))
                    ? labels.Select(item => item!).ToArray()
                    : [];
            }

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                var labels = document.RootElement.EnumerateObject()
                    .Where(item => int.TryParse(item.Name, out _))
                    .Select(item => (
                        Index: int.Parse(item.Name),
                        Name: item.Value.ValueKind == JsonValueKind.String ? item.Value.GetString() : null
                    ))
                    .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                    .OrderBy(item => item.Index)
                    .ToArray();
                if (labels.Select((item, index) => item.Index == index).All(item => item))
                    return labels.Select(item => item.Name!).ToArray();
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
        }

        var indexedLabels = Regex.Matches(value, @"(?<index>\d+)\s*:\s*'(?<name>(?:\\.|[^'])*)'")
            .Select(match =>
            {
                var name = match.Groups["name"].Value.Replace("\\'", "'").Replace("\\\\", "\\");
                return (Index: int.Parse(match.Groups["index"].Value), Name: name);
            })
            .OrderBy(item => item.Index)
            .ToArray();

        return indexedLabels.Length > 0
            && indexedLabels.Select((item, index) => item.Index == index).All(item => item)
            ? indexedLabels.Select(item => item.Name).ToArray()
            : [];
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
