namespace LyuOnnxCore.Helpers;

internal static class DetectionLabelHelper
{
    public static (string[] ModelLabels, HashSet<string> RequestedLabels) Prepare(
        string modelPath,
        IEnumerable<string> labels
    )
    {
        var requestedLabels = labels?
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Select(static label => label.Trim())
            .ToHashSet(StringComparer.Ordinal)
            ?? [];

        if (requestedLabels.Count == 0)
        {
            throw new ArgumentException("At least one label is required.", nameof(labels));
        }

        var modelLabels = OnnxModelHelper.GetModelLabels(modelPath);
        if (modelLabels.Length == 0)
        {
            throw new InvalidOperationException(
                $"The model does not contain a readable label list: {modelPath}. Add ONNX names metadata or a classes.txt sidecar file."
            );
        }

        var unknownLabels = requestedLabels
            .Where(label => !modelLabels.Contains(label, StringComparer.Ordinal))
            .ToArray();
        if (unknownLabels.Length > 0)
        {
            throw new ArgumentException(
                $"The requested labels are not present in the model: {string.Join(", ", unknownLabels)}.",
                nameof(labels)
            );
        }

        return (modelLabels, requestedLabels);
    }
}
