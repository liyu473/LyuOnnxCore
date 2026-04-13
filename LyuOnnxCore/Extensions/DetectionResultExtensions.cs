using LyuOnnxCore.Models;

namespace LyuOnnxCore.Extensions;

public static class HbbDetectionResultExtensions
{
    public static List<HbbDetectionResult> FilterOverlapping(
        this IEnumerable<HbbDetectionResult> detections,
        float overlapThreshold = 0.5f,
        bool crossClass = false
    )
    {
        var sorted = detections?
            .OrderByDescending(static detection => detection.Confidence)
            .ToList()
            ?? [];

        if (sorted.Count == 0)
        {
            return [];
        }

        var result = new List<HbbDetectionResult>();
        while (sorted.Count > 0)
        {
            var best = sorted[0];
            result.Add(best);
            sorted.RemoveAt(0);

            sorted = [.. sorted.Where(detection =>
            {
                if (!crossClass && detection.LabelIndex != best.LabelIndex)
                {
                    return true;
                }

                float iou = CalculateIoU(best.BoundingBox, detection.BoundingBox);
                return iou < overlapThreshold;
            })];
        }

        return result;
    }

    public static List<HbbDetectionResult> FilterContained(
        this IEnumerable<HbbDetectionResult> detections,
        float containThreshold = 0.8f,
        bool crossClass = false
    )
    {
        var detectionList = detections?.ToList() ?? [];
        if (detectionList.Count == 0)
        {
            return [];
        }

        var result = new List<HbbDetectionResult>();
        foreach (var detection in detectionList)
        {
            bool isContained = false;
            foreach (var other in detectionList)
            {
                if (ReferenceEquals(detection, other))
                {
                    continue;
                }

                if (!crossClass && detection.LabelIndex != other.LabelIndex)
                {
                    continue;
                }

                float containRatio = CalculateContainRatio(detection.BoundingBox, other.BoundingBox);
                if (containRatio >= containThreshold && detection.Confidence <= other.Confidence)
                {
                    isContained = true;
                    break;
                }
            }

            if (!isContained)
            {
                result.Add(detection);
            }
        }

        return result;
    }

    public static List<HbbDetectionResult> FilterByConfidence(
        this IEnumerable<HbbDetectionResult> detections,
        float minConfidence
    )
    {
        return [.. detections.Where(detection => detection.Confidence >= minConfidence)];
    }

    public static List<HbbDetectionResult> FilterByLabels(
        this IEnumerable<HbbDetectionResult> detections,
        params string[] labels
    )
    {
        return [.. detections.Where(detection => labels.Contains(detection.LabelName))];
    }

    public static List<HbbDetectionResult> ExcludeLabels(
        this IEnumerable<HbbDetectionResult> detections,
        params string[] labels
    )
    {
        return [.. detections.Where(detection => !labels.Contains(detection.LabelName))];
    }

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

    private static float CalculateContainRatio(BoundingBox box1, BoundingBox box2)
    {
        int x1 = Math.Max(box1.X, box2.X);
        int y1 = Math.Max(box1.Y, box2.Y);
        int x2 = Math.Min(box1.Right, box2.Right);
        int y2 = Math.Min(box1.Bottom, box2.Bottom);

        int intersectionArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        return box1.Area > 0 ? (float)intersectionArea / box1.Area : 0;
    }
}

public static class ObbDetectionResultExtensions
{
    public static List<ObbDetectionResult> FilterContained(
        this IEnumerable<ObbDetectionResult> detections,
        float containThreshold = 0.8f,
        bool crossClass = false
    )
    {
        var detectionList = detections?.ToList() ?? [];
        if (detectionList.Count == 0)
        {
            return [];
        }

        var result = new List<ObbDetectionResult>();
        foreach (var detection in detectionList)
        {
            bool isContained = false;
            foreach (var other in detectionList)
            {
                if (ReferenceEquals(detection, other))
                {
                    continue;
                }

                if (!crossClass && detection.LabelIndex != other.LabelIndex)
                {
                    continue;
                }

                var box1 = detection.OrientedBoundingBox.GetBoundingBox();
                var box2 = other.OrientedBoundingBox.GetBoundingBox();
                float containRatio = CalculateContainRatio(box1, box2);
                if (containRatio >= containThreshold && detection.Confidence <= other.Confidence)
                {
                    isContained = true;
                    break;
                }
            }

            if (!isContained)
            {
                result.Add(detection);
            }
        }

        return result;
    }

    private static float CalculateContainRatio(BoundingBox box1, BoundingBox box2)
    {
        int x1 = Math.Max(box1.X, box2.X);
        int y1 = Math.Max(box1.Y, box2.Y);
        int x2 = Math.Min(box1.Right, box2.Right);
        int y2 = Math.Min(box1.Bottom, box2.Bottom);

        int intersectionArea = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
        return box1.Area > 0 ? (float)intersectionArea / box1.Area : 0;
    }
}
