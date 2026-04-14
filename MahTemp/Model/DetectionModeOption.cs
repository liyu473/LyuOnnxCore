namespace MahTemp.Model;

public enum DetectionMode
{
    YoloXHbb,
    YoloHbb,
    YoloObb,
}

public sealed class DetectionModeOption
{
    public DetectionModeOption(DetectionMode mode, string displayName, string description)
    {
        Mode = mode;
        DisplayName = displayName;
        Description = description;
    }

    public DetectionMode Mode { get; }

    public string DisplayName { get; }

    public string Description { get; }

    public bool IsObb => Mode == DetectionMode.YoloObb;
}
