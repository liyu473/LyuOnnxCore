namespace LyuOnnxCore.Models;

/// <summary>
/// ONNX 检测模型类型。
/// </summary>
public enum OnnxModelType
{
    Unknown = 0,
    YoloHbb = 1,
    YoloObb = 2,
    YoloXHbb = 3
}
