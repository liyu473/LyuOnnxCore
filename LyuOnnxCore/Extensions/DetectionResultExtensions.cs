using LyuOnnxCore.Models;

namespace LyuOnnxCore.Extensions;

/// <summary>
/// DetectionResult 列表扩展方法
/// </summary>
public static class DetectionResultExtensions
{
    /// <summary>
    /// 过滤重叠的检测框，保留置信度较高的框
    /// </summary>
    /// <param name="detections">检测结果列表</param>
    /// <param name="overlapThreshold">重叠度阈值 (0-1)，超过此阈值的框将被过滤</param>
    /// <param name="crossClass">是否跨类别过滤，默认 false 只过滤同类别的重叠框</param>
    /// <returns>过滤后的检测结果列表</returns>
    public static List<DetectionResult> FilterOverlapping(
        this List<DetectionResult> detections,
        float overlapThreshold = 0.5f,
        bool crossClass = false)
    {
        if (detections == null || detections.Count == 0)
            return [];

        // 按置信度降序排序
        var sorted = detections.OrderByDescending(d => d.Confidence).ToList();
        var result = new List<DetectionResult>();

        while (sorted.Count > 0)
        {
            // 取出置信度最高的框
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);

            // 移除与当前框重叠度超过阈值的框
            sorted = [.. sorted.Where(d =>
            {
                // 如果不跨类别过滤，只比较同类别的框
                if (!crossClass && d.LabelIndex != best.LabelIndex)
                    return true;

                float iou = CalculateIoU(best.BoundingBox, d.BoundingBox);
                return iou < overlapThreshold;
            })];
        }

        return result;
    }

    /// <summary>
    /// 过滤被包含的小框（当一个框被另一个框包含超过指定比例时移除）
    /// </summary>
    /// <param name="detections">检测结果列表</param>
    /// <param name="containThreshold">包含度阈值 (0-1)，小框被大框包含超过此比例时被过滤</param>
    /// <param name="crossClass">是否跨类别过滤</param>
    /// <returns>过滤后的检测结果列表</returns>
    public static List<DetectionResult> FilterContained(
        this List<DetectionResult> detections,
        float containThreshold = 0.8f,
        bool crossClass = false)
    {
        if (detections == null || detections.Count == 0)
            return [];

        var result = new List<DetectionResult>();

        foreach (var detection in detections)
        {
            bool isContained = false;

            foreach (var other in detections)
            {
                if (ReferenceEquals(detection, other))
                    continue;

                // 如果不跨类别过滤，只比较同类别的框
                if (!crossClass && detection.LabelIndex != other.LabelIndex)
                    continue;

                // 检查 detection 是否被 other 包含
                float containRatio = CalculateContainRatio(detection.BoundingBox, other.BoundingBox);
                if (containRatio >= containThreshold)
                {
                    // 如果被包含且置信度较低，则过滤掉
                    if (detection.Confidence <= other.Confidence)
                    {
                        isContained = true;
                        break;
                    }
                }
            }

            if (!isContained)
                result.Add(detection);
        }

        return result;
    }

    /// <summary>
    /// 按置信度过滤
    /// </summary>
    /// <param name="detections">检测结果列表</param>
    /// <param name="minConfidence">最小置信度阈值</param>
    /// <returns>过滤后的检测结果列表</returns>
    public static List<DetectionResult> FilterByConfidence(
        this List<DetectionResult> detections,
        float minConfidence)
    {
        return [.. detections.Where(d => d.Confidence >= minConfidence)];
    }

    /// <summary>
    /// 按标签过滤
    /// </summary>
    /// <param name="detections">检测结果列表</param>
    /// <param name="labels">要保留的标签名称</param>
    /// <returns>过滤后的检测结果列表</returns>
    public static List<DetectionResult> FilterByLabels(
        this List<DetectionResult> detections,
        params string[] labels)
    {
        return [.. detections.Where(d => labels.Contains(d.LabelName))];
    }

    /// <summary>
    /// 排除指定标签
    /// </summary>
    /// <param name="detections">检测结果列表</param>
    /// <param name="labels">要排除的标签名称</param>
    /// <returns>过滤后的检测结果列表</returns>
    public static List<DetectionResult> ExcludeLabels(
        this List<DetectionResult> detections,
        params string[] labels)
    {
        return [.. detections.Where(d => !labels.Contains(d.LabelName))];
    }

    #region 私有方法

    /// <summary>
    /// 计算两个框的 IoU (Intersection over Union)
    /// </summary>
    private static float CalculateIoU(BoundingBox box1, BoundingBox box2)
    {
        int x1 = Math.Max(box1.X, box2.X);
        int y1 = Math.Max(box1.Y, box2.Y);
        int x2 = Math.Min(box1.Right, box2.Right);
        int y2 = Math.Min(box1.Bottom, box2.Bottom);

        int intersectionArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        int unionArea = box1.Area + box2.Area - intersectionArea;

        return unionArea > 0 ? (float)intersectionArea / unionArea : 0;
    }

    /// <summary>
    /// 计算 box1 被 box2 包含的比例
    /// </summary>
    private static float CalculateContainRatio(BoundingBox box1, BoundingBox box2)
    {
        int x1 = Math.Max(box1.X, box2.X);
        int y1 = Math.Max(box1.Y, box2.Y);
        int x2 = Math.Min(box1.Right, box2.Right);
        int y2 = Math.Min(box1.Bottom, box2.Bottom);

        int intersectionArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);

        return box1.Area > 0 ? (float)intersectionArea / box1.Area : 0;
    }

    #endregion
}
