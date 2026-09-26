# LyuOnnxCore

[![NuGet](https://img.shields.io/badge/NuGet-2.0.1-blue.svg)](https://www.nuget.org/packages/LyuOnnxCore)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

基于 ONNX Runtime 和 OpenCvSharp 的 YOLO 检测类库，支持：

- YOLO 水平边界框检测（HBB）
- YOLO 旋转边界框检测（OBB）
- YOLOX 水平边界框检测
- 置信度、NMS、重叠框过滤和结果绘制
- 从 ONNX 元数据或标签文件读取模型类别

当前包版本为 `2.0.1`。此版本调整了检测服务的标签参数语义，不兼容旧版调用方式。

## 安装

```bash
dotnet add package LyuOnnxCore --version 2.0.1
```

## 标签读取与筛选

模型输出中的类别索引由训练过程固定，标签名称的顺序必须与类别 ID 一致。检测服务会自动读取模型的完整标签表，调用方只需要传入想要返回的标签：

```csharp
var labels = new[] { "ErrorMark" };
var results = yoloHbbDetectionService.Detect(
    modelPath,
    image,
    labels,
    new DetectionOptions
    {
        ConfidenceThreshold = 0.25f
    });
```

这里的 `labels` 是筛选条件，不是模型的完整类别表。即使只检测一个类别，也不会改变模型类别索引映射。

服务按以下顺序读取完整标签表：

1. ONNX 的 `CustomMetadataMap["names"]` 元数据
2. 与模型同名的 `.txt` 文件，例如 `best.txt`
3. 模型目录下的 `classes.txt`

标签文件每行一个类别，并且顺序必须与训练时的类别 ID 一致。没有可读取的完整标签表时，检测会抛出异常，避免产生错误类别结果。

也可以直接读取标签列表：

```csharp
var modelLabels = OnnxModelHelper.GetModelLabels(modelPath);
```

## 注册检测服务

```csharp
using LyuOnnxCore.Register;

services.AddOnnxDetectionServices();
```

也可以只注册需要的服务：

```csharp
services.AddYoloHbbDetectionService();
services.AddYoloObbDetectionService();
services.AddYoloXHbbDetectionService();
```

## HBB 检测

```csharp
using LyuOnnxCore.Interfaces;
using LyuOnnxCore.Models;
using OpenCvSharp;

var image = Cv2.ImRead("image.jpg");
var labels = new[] { "ErrorMark", "Scratch" };

var results = yoloHbbDetectionService.Detect(
    "best.onnx",
    image,
    labels,
    new DetectionOptions
    {
        ConfidenceThreshold = 0.25f,
        NmsThreshold = 0.45f,
        IsFilterOverlay = true,
        IsCrossClass = true,
        OverlayThreshold = 0.8f
    });
```

`Detect` 同时提供图像路径和 `Mat` 两种重载。`IYoloXHbbDetectionService` 的调用方式相同，适用于 YOLOX 模型。

## OBB 检测

```csharp
var results = yoloObbDetectionService.Detect(
    "best-obb.onnx",
    image,
    new[] { "Part" },
    new DetectionOptions());
```

## 检测并绘制

```csharp
var outputImage = yoloHbbDetectionService.DetectAndVisualize(
    "best.onnx",
    image,
    new[] { "ErrorMark" },
    new DetectionOptions(),
    new DrawOptions
    {
        ShowLabel = true,
        ShowConfidence = true,
        BoxThickness = 2
    });
```

OBB 服务使用相同的 `DetectAndVisualize` 方法，并绘制旋转边界框。

## 检测选项

```csharp
var options = new DetectionOptions
{
    ConfidenceThreshold = 0.25f,
    NmsThreshold = 0.45f,
    InputWidth = 640,
    InputHeight = 640,
    IsFilterOverlay = true,
    IsCrossClass = true,
    OverlayThreshold = 0.8f
};
```

`DetectionOptions` 不再包含 `FilterLabels`。标签筛选统一通过 `Detect` 的 `labels` 参数完成。

## 模型信息

```csharp
var models = OnnxModelHelper.GetOnnxModels(@"D:\models");
foreach (var model in models)
{
    Console.WriteLine($"{model.Name}: {string.Join(", ", model.Labels)}");
}
```

`OnnxModelInfo.Labels` 是按模型类别 ID 排列的完整标签列表，可用于显示标签选择控件。传给检测服务时，只传用户选中的标签即可。

模型类型可以通过以下 API 读取：

```csharp
var modelType = OnnxModelHelper.GetModelType(modelPath);
// OnnxModelType.YoloHbb、YoloObb、YoloXHbb 或 Unknown
```


## 低级扩展方法

`InferenceSession` 上的 `Detect`、`DetectOBB` 和 `DetectYoloX` 是底层扩展方法。直接使用这些方法时，`labels` 必须仍然是按模型类别 ID 顺序排列的完整标签表；需要按用户选择筛选时，请使用上面的三个检测服务接口。

## 模型导出建议

导出 YOLO ONNX 时应保留类别名称。若导出工具没有写入 ONNX `names` 元数据，请在模型旁放置 `classes.txt`，每行写一个训练类别名称，顺序与训练配置一致。

## 运行环境

- .NET 8 或 .NET 10
- Windows 下使用 WPF 时需要对应的 OpenCvSharp Windows runtime
- 类库依赖 Microsoft.ML.OnnxRuntime、OpenCvSharp4
