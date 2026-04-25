# Calibration 使用说明

本文介绍 `LyuOnnxCore.Calibration` 下三个接口的典型用法：

- `ICameraCalibration`：相机内参标定
- `INinePointCalibration`：像素坐标到平台坐标的平面映射
- `IAxisPositionCompensation`：结合当前轴位置做偏移补偿

## 1. 注册服务

如果你在项目里使用 `Microsoft.Extensions.DependencyInjection`，可以先注册标定服务：

```csharp
using LyuOnnxCore.Calibration.Register;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddCalibrationServices();

var serviceProvider = services.BuildServiceProvider();
```

获取服务：

```csharp
var cameraCalibration = serviceProvider.GetRequiredService<ICameraCalibration>();
var ninePointCalibration = serviceProvider.GetRequiredService<INinePointCalibration>();
var axisCompensation = serviceProvider.GetRequiredService<IAxisPositionCompensation>();
```

## 2. 相机标定

`ICameraCalibration` 支持两种输入：

- 传入图片路径集合
- 传入 `OpenCvSharp.Mat` 集合

支持三种标定板：

- `CalibrationPatternType.Chessboard`：棋盘格
- `CalibrationPatternType.SymmetricCirclesGrid`：对称圆点板 / 圆形孔标定板
- `CalibrationPatternType.AsymmetricCirclesGrid`：非对称圆点板 / 圆形孔标定板

方法签名：

```csharp
CameraCalibrateResult Calibrate(
    IEnumerable<string> imagePaths,
    Size patternSize,
    float squareSizeMm
);

CameraCalibrateResult Calibrate(
    IEnumerable<Mat> images,
    Size patternSize,
    float squareSizeMm
);

CameraCalibrateResult Calibrate(
    IEnumerable<string> imagePaths,
    Size patternSize,
    float pointSpacingMm,
    CalibrationPatternType patternType
);

CameraCalibrateResult Calibrate(
    IEnumerable<Mat> images,
    Size patternSize,
    float pointSpacingMm,
    CalibrationPatternType patternType
);
```

参数说明：

- `patternSize`：棋盘格时表示内角点数量；圆点板时表示圆心数量；`Width` 是列数，`Height` 是行数
- `squareSizeMm`：棋盘格单个方格的实际边长，单位毫米
- `pointSpacingMm`：相邻标定点的实际间距；棋盘格为内角点间距，圆点板为圆心距，单位毫米
- `patternType`：标定板类型，不传时默认使用棋盘格，兼容旧调用
- 非对称圆点板按 OpenCV 的点位模型生成物理点：同一行相邻圆心间距为 `2 * pointSpacingMm`，相邻行错位 `pointSpacingMm`

棋盘格示例：

```csharp
using LyuOnnxCore.Calibration.Interface;
using OpenCvSharp;

var imagePaths = new[]
{
    @"D:\calibration\01.jpg",
    @"D:\calibration\02.jpg",
    @"D:\calibration\03.jpg",
    @"D:\calibration\04.jpg",
};

var result = cameraCalibration.Calibrate(
    imagePaths,
    new Size(9, 6),
    30f
);

Console.WriteLine($"ImageSize: {result.ImageSize.Width} x {result.ImageSize.Height}");
Console.WriteLine($"ReprojectionError: {result.ReprojectionError:F4}");
Console.WriteLine($"SuccessfulImageCount: {result.SuccessfulImageCount}");
```

圆点板示例：

```csharp
using LyuOnnxCore.Calibration.Interface;
using LyuOnnxCore.Calibration.Models;
using OpenCvSharp;

var result = cameraCalibration.Calibrate(
    imagePaths,
    new Size(11, 8),
    20f,
    CalibrationPatternType.SymmetricCirclesGrid
);
```

`CameraCalibrateResult` 主要字段：

- `ImageSize`：参与标定的图像分辨率
- `PatternType`：参与标定的标定板类型
- `CameraMatrix`：3x3 相机内参矩阵
- `CameraMatrixFlat`：扁平化后的相机内参，便于序列化和反序列化
- `DistortionCoefficients`：畸变系数
- `RotationVectors` / `TranslationVectors`：每张图的外参
- `ReprojectionError`：整体重投影误差
- `PerViewErrors`：每张图的重投影误差
- `SuccessfulImageCount`：成功参与标定的图片数
- `InputImageCount`：输入图片总数
- `SkippedMismatchedResolutionCount`：因分辨率不一致而跳过的数量

### 相机标定结果序列化

`ICameraCalibration` 现在也负责 `CameraCalibrateResult` 的 JSON 序列化和反序列化：

```csharp
string SerializeResult(
    CameraCalibrateResult result,
    bool writeIndented = true
);

CameraCalibrateResult DeserializeResult(string json);
```

示例：

```csharp
var json = cameraCalibration.SerializeResult(result);
var restored = cameraCalibration.DeserializeResult(json);
```

### 相机标定建议

- 建议至少准备 6 到 10 张不同角度的标定板图片
- 所有标定图尽量使用相同分辨率
- 标定板尽量覆盖画面不同区域，不要都只在中间
- 棋盘格的 `patternSize` 要传内角点数，不是黑白格总数
- 圆点板的 `patternSize` 要传圆心数量，不是圆孔外框数量

## 3. 九点标定

九点标定用于建立图像像素坐标与平台坐标之间的映射关系。

方法签名：

```csharp
NinePointCalibrationResult Calibrate(
    IList<CalibrationPair> pairs
);

NinePointCalibrationResult Calibrate(
    CameraCalibrateResult cameraCalibration,
    IList<CalibrationPair> pairs
);

Point2d PixelToWorld(
    Point2d pixelPoint,
    NinePointCalibrationResult ninePointCalibration
);

Point2d PixelToWorld(
    Point2d pixelPoint,
    CameraCalibrateResult cameraCalibration,
    NinePointCalibrationResult ninePointCalibration
);
```

其中 `CalibrationPair` 定义如下：

```csharp
public class CalibrationPair
{
    public Point2d ImagePoint { get; set; }
    public Point2d WorldPoint { get; set; }
}
```

约定说明：

- `ImagePoint`：图像中的像素坐标
- `WorldPoint`：平台坐标
- 当前实现里 `WorldPoint` 推荐直接使用脉冲值
- 至少需要 4 组点，推荐 9 组或更多
- 如果传入 `CameraCalibrateResult`，会先做去畸变再拟合
- 如果不传入 `CameraCalibrateResult`，会直接使用原始像素坐标拟合

示例：

```csharp
using LyuOnnxCore.Calibration.Models;
using OpenCvSharp;

var pairs = new List<CalibrationPair>
{
    new() { ImagePoint = new Point2d(102.3, 118.6), WorldPoint = new Point2d(1000, 2000) },
    new() { ImagePoint = new Point2d(325.1, 120.4), WorldPoint = new Point2d(5000, 2000) },
    new() { ImagePoint = new Point2d(546.9, 122.7), WorldPoint = new Point2d(9000, 2000) },
    new() { ImagePoint = new Point2d(104.4, 301.2), WorldPoint = new Point2d(1000, 6000) },
    new() { ImagePoint = new Point2d(327.0, 302.6), WorldPoint = new Point2d(5000, 6000) },
    new() { ImagePoint = new Point2d(549.5, 304.0), WorldPoint = new Point2d(9000, 6000) },
    new() { ImagePoint = new Point2d(106.2, 484.8), WorldPoint = new Point2d(1000, 10000) },
    new() { ImagePoint = new Point2d(329.4, 485.7), WorldPoint = new Point2d(5000, 10000) },
    new() { ImagePoint = new Point2d(551.7, 487.3), WorldPoint = new Point2d(9000, 10000) },
};

var ninePointResult = ninePointCalibration.Calibrate(result, pairs);

var worldPoint = ninePointCalibration.PixelToWorld(
    new Point2d(330.0, 305.0),
    result,
    ninePointResult
);

Console.WriteLine($"WorldX: {worldPoint.X:F2}, WorldY: {worldPoint.Y:F2}");
```

不依赖相机内参时，也可以直接这样用：

```csharp
var ninePointResult = ninePointCalibration.Calibrate(pairs);

var worldPoint = ninePointCalibration.PixelToWorld(
    new Point2d(330.0, 305.0),
    ninePointResult
);
```

`NinePointCalibrationResult` 主要字段：

- `PixelToWorldTransform`：像素到平台的 3x3 变换矩阵
- `WorldToPixelTransform`：逆变换矩阵
- `CameraMatrix`：九点标定时保存的相机矩阵快照
- `DistortionCoefficients`：九点标定时保存的畸变快照
- `MeanReprojectionError`：平均映射误差
- `MaxReprojectionError`：最大映射误差
- `PairCount`：输入点对数
- `InlierCount`：RANSAC 内点数
- `Pairs`：参与标定的点对

补充说明：

- `PixelToWorldTransform` 的输入必须是去畸变后的参考像素
- `INinePointCalibration.PixelToWorld(...)` 内部已经先做了去畸变，再进入该变换
- `NinePointCalibrationResult` 中的扁平矩阵字段现在可直接用于序列化和反序列化
- 如果九点结果是通过不带内参的重载生成的，那么 `CameraMatrix` 会为空，`DistortionCoefficients` 也会为空

### 九点标定结果序列化

`INinePointCalibration` 也提供了结果对象的 JSON 序列化和反序列化：

```csharp
string SerializeResult(
    NinePointCalibrationResult result,
    bool writeIndented = true
);

NinePointCalibrationResult DeserializeResult(string json);
```

示例：

```csharp
var json = ninePointCalibration.SerializeResult(ninePointResult);
var restored = ninePointCalibration.DeserializeResult(json);
```

## 4. 轴位置补偿

当标定完成后，如果平台相对标定零位发生了偏移，可以使用 `IAxisPositionCompensation` 根据当前轴位置做补偿。

方法签名：

```csharp
Point2d TransformToCalibrationPixel(
    Point2d detectedPixel,
    Point2d currentAxisPosition,
    Point2d calibrationAxisZero,
    NinePointCalibrationResult ninePointCalibration
);

Point2d PixelToWorldWithCompensation(
    Point2d detectedPixel,
    Point2d currentAxisPosition,
    Point2d calibrationAxisZero,
    NinePointCalibrationResult ninePointCalibration
);
```

参数说明：

- `detectedPixel`：当前检测到的像素点
- `currentAxisPosition`：当前平台轴坐标，单位通常是脉冲
- `calibrationAxisZero`：做九点标定时记录的轴零位
- `ninePointCalibration`：九点标定结果

示例：

```csharp
var calibrationAxisZero = new Point2d(50000, 80000);
var currentAxisPosition = new Point2d(50120, 79860);
var detectedPixel = new Point2d(330.5, 305.7);

var calibrationPixel = axisCompensation.TransformToCalibrationPixel(
    detectedPixel,
    currentAxisPosition,
    calibrationAxisZero,
    ninePointResult
);

var compensatedWorld = axisCompensation.PixelToWorldWithCompensation(
    detectedPixel,
    currentAxisPosition,
    calibrationAxisZero,
    ninePointResult
);
```

使用建议：

- `currentAxisPosition` 和 `calibrationAxisZero` 必须使用同一坐标系、同一单位
- 如果 `WorldPoint` 使用的是脉冲值，这两个轴位置也应该使用脉冲值
- 如果后续改成毫米坐标，三者单位也要保持一致
- 当前补偿模型依赖“九点标定平面 = 运行时工作平面”的假设
- 目标高度变化、相机姿态变化、或离开标定平面后，补偿结果可能失真
- `TransformToCalibrationPixel(...)` 返回的是标定参考系下的去畸变像素，不是原始图像像素

## 5. 推荐使用流程

推荐按下面顺序使用：

1. 采集棋盘格或圆点板图片，调用 `ICameraCalibration` 做相机标定
2. 采集九点或更多点位，调用 `INinePointCalibration` 建立像素到平台的映射
3. 正常运行时，将检测结果像素点传给 `PixelToWorld`
4. 如果平台相对标定零位有偏移，再使用 `IAxisPositionCompensation`

## 6. 常见问题

### 1. 为什么相机标定失败

常见原因：

- 标定板参数设置错误
- 图片数量太少
- 图片分辨率不一致
- 标定板没有被完整拍到
- 图像模糊，角点提取不稳定

### 2. 为什么九点标定误差很大

常见原因：

- 点位采集不均匀，过于集中
- 平台坐标录入错误
- 使用九点时镜头还没有先做相机标定
- 实际平台有倾斜，单应矩阵无法完全表达真实关系

### 3. `TransformToCalibrationPixel` 和 `PixelToWorldWithCompensation` 区别是什么

- `TransformToCalibrationPixel` 返回标定参考系下的去畸变像素坐标
- `PixelToWorldWithCompensation` 返回补偿后的平台坐标

如果你后面还要自己做别的平面映射，用前者；如果你要直接下发运动坐标，用后者。
