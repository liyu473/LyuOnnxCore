# LyuOnnxCore

[![NuGet](https://img.shields.io/badge/NuGet-1.0.2-blue.svg)](https://www.nuget.org/packages/LyuOnnxCore)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

一个基于 ONNX Runtime 和 OpenCvSharp 的 YOLO 目标检测库，支持标准目标检测和旋转边界框（OBB）检测。

**注：仅支持WPf，WPF类库**



## 🚀 快速开始

### 标准目标检测

```csharp
using LyuOnnxCore.Extensions;
using LyuOnnxCore.Models;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

// 1. 加载 ONNX 模型
var session = new InferenceSession("yolov8n.onnx");

// 2. 读取图像
var image = Cv2.ImRead("image.jpg");

// 3. 定义标签
var labels = new[] { "person", "car", "dog", "cat" };

// 4. 执行检测
var results = session.Detect(image, labels);

// 5. 绘制结果
var outputImage = image.DrawDetections(results);
Cv2.ImShow("Result", outputImage);
Cv2.WaitKey(0);
```

### 一行代码检测并绘制

```csharp
var outputImage = session.DetectAndDraw(image, labels);
```

### OBB 检测（旋转边界框）

```csharp
// 使用 YOLOv8-OBB 模型
var session = new InferenceSession("yolov8n-obb.onnx");
var image = Cv2.ImRead("image.jpg");
var labels = new[] { "object1", "object2" };

// 执行 OBB 检测并绘制
var outputImage = session.DetectOBBAndDraw(image, labels);
Cv2.ImShow("OBB Result", outputImage);
Cv2.WaitKey(0);
```

## ⚙️ 高级配置

### 检测选项

```csharp
var options = new DetectionOptions
{
    ConfidenceThreshold = 0.25f,    // 置信度阈值
    NmsThreshold = 0.45f,            // NMS 阈值
    InputWidth = 640,                 // 模型输入宽度（null 时自动获取）
    InputHeight = 640,                // 模型输入高度（null 时自动获取）
    FilterLabels = new[] { "person", "car" },  // 只返回指定标签
    IsFilterOverlay = true,           // 启用重叠框过滤
    IsCrossClass = true,              // 跨类别过滤重叠框
    OverlayThreshold = 0.8f           // 重叠阈值
};

var results = session.Detect(image, labels, options);
```

### 绘制选项

```csharp
var drawOptions = new DrawOptions
{
    BoxColor = System.Drawing.Color.Green,  // 边界框颜色
    BoxThickness = 2,                        // 边界框线宽
    TextColor = System.Drawing.Color.White,  // 文本颜色
    FontScale = 0.5,                         // 字体大小
    ShowLabel = true,                        // 显示标签名称
    ShowConfidence = true,                   // 显示置信度
    UseChineseFont = false,                  // 使用中文字体
    ChineseFontFamily = "微软雅黑",          // 中文字体
    ChineseFontSize = 20                     // 中文字体大小
};

var outputImage = image.DrawDetections(results, drawOptions);
```

### 中文标签支持

```csharp
var labels = new[] { "人", "汽车", "狗", "猫" };

var drawOptions = new DrawOptions
{
    UseChineseFont = true,
    ChineseFontFamily = "微软雅黑",
    ChineseFontSize = 20
};

var outputImage = session.DetectAndDraw(image, labels, null, drawOptions);
```

## 📚 API 文档

### 扩展方法

#### `Detect()`
执行标准目标检测

```csharp
List<DetectionResult> Detect(
    this InferenceSession session,
    Mat image,
    string[] labels,
    DetectionOptions? options = null)
```

#### `DetectOBB()`
执行 OBB（旋转边界框）检测

```csharp
List<DetectionResult> DetectOBB(
    this InferenceSession session,
    Mat image,
    string[] labels,
    DetectionOptions? options = null)
```

#### `DrawDetections()`
绘制标准检测结果

```csharp
Mat DrawDetections(
    this Mat image,
    List<DetectionResult> detections,
    DrawOptions? options = null)
```

#### `DrawOBBDetections()`
绘制 OBB 检测结果

```csharp
Mat DrawOBBDetections(
    this Mat image,
    List<DetectionResult> detections,
    DrawOptions? options = null)
```


