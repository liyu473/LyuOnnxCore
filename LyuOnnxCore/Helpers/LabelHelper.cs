using System.IO;
using System.ComponentModel;
using System.Reflection;

namespace LyuOnnxCore.Helpers;

/// <summary>
/// 标签管理辅助类
/// </summary>
public static class LabelHelper
{
    /// <summary>
    /// 从文本文件加载标签列表（每行一个标签）
    /// </summary>
    /// <param name="filePath">标签文件路径</param>
    /// <returns>标签数组</returns>
    public static string[] LoadLabelsFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"标签文件不存在: {filePath}");

        var labels = File.ReadAllLines(filePath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToArray();

        if (labels.Length == 0)
            throw new InvalidOperationException($"标签文件为空: {filePath}");

        return labels;
    }

    /// <summary>
    /// 从文本文件异步加载标签列表
    /// </summary>
    /// <param name="filePath">标签文件路径</param>
    /// <returns>标签数组</returns>
    public static async Task<string[]> LoadLabelsFromFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"标签文件不存在: {filePath}");

        var lines = await File.ReadAllLinesAsync(filePath);
        var labels = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToArray();

        if (labels.Length == 0)
            throw new InvalidOperationException($"标签文件为空: {filePath}");

        return labels;
    }

    /// <summary>
    /// 将标签列表保存到文本文件（每行一个标签）
    /// </summary>
    /// <param name="labels">标签数组</param>
    /// <param name="filePath">保存路径</param>
    public static void SaveLabelsToFile(string[] labels, string filePath)
    {
        if (labels == null || labels.Length == 0)
            throw new ArgumentException("标签数组不能为空", nameof(labels));

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        File.WriteAllLines(filePath, labels);
    }

    /// <summary>
    /// 异步将标签列表保存到文本文件
    /// </summary>
    /// <param name="labels">标签数组</param>
    /// <param name="filePath">保存路径</param>
    public static async Task SaveLabelsToFileAsync(string[] labels, string filePath)
    {
        if (labels == null || labels.Length == 0)
            throw new ArgumentException("标签数组不能为空", nameof(labels));

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("文件路径不能为空", nameof(filePath));

        await File.WriteAllLinesAsync(filePath, labels);
    }

    /// <summary>
    /// 从逗号分隔的字符串创建标签数组
    /// </summary>
    /// <param name="labelsString">逗号分隔的标签字符串</param>
    /// <returns>标签数组</returns>
    public static string[] ParseLabels(string labelsString)
    {
        if (string.IsNullOrWhiteSpace(labelsString))
            throw new ArgumentException("标签字符串不能为空", nameof(labelsString));

        return [.. labelsString
            .Split(',')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))];
    }

    /// <summary>
    /// 从枚举类型获取标签名称数组
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <returns>标签名称数组</returns>
    public static string[] GetLabelsFromEnum<T>() where T : Enum
    {
        return Enum.GetNames(typeof(T));
    }

    /// <summary>
    /// 从枚举类型获取描述列表
    /// </summary>
    /// <typeparam name="T">枚举类型</typeparam>
    /// <returns>描述字符串数组</returns>
    public static string[] GetLabelsFromEnumDescription<T>() where T : Enum
    {
        var type = typeof(T);
        var names = Enum.GetNames(type);
        var descriptions = new string[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            var field = type.GetField(names[i]);
            var attr = field?.GetCustomAttribute<DescriptionAttribute>();
            descriptions[i] = attr?.Description ?? names[i];
        }

        return descriptions;
    }

    /// <summary>
    /// 验证标签数组是否有效
    /// </summary>
    /// <param name="labels">标签数组</param>
    /// <returns>是否有效</returns>
    public static bool ValidateLabels(string[]? labels)
    {
        return labels != null && labels.Length > 0 && labels.All(l => !string.IsNullOrWhiteSpace(l));
    }

    /// <summary>
    /// 根据索引获取标签名称
    /// </summary>
    /// <param name="labels">标签数组</param>
    /// <param name="index">索引</param>
    /// <returns>标签名称，如果索引无效返回 "Unknown"</returns>
    public static string GetLabelByIndex(string[] labels, int index)
    {
        if (labels == null || index < 0 || index >= labels.Length)
            return "Unknown";

        return labels[index];
    }

    /// <summary>
    /// 根据标签名称获取索引
    /// </summary>
    /// <param name="labels">标签数组</param>
    /// <param name="labelName">标签名称</param>
    /// <returns>索引，如果未找到返回 -1</returns>
    public static int GetIndexByLabel(string[] labels, string labelName)
    {
        if (labels == null || string.IsNullOrWhiteSpace(labelName))
            return -1;

        return Array.FindIndex(labels, l => l.Equals(labelName, StringComparison.OrdinalIgnoreCase));
    }
}
