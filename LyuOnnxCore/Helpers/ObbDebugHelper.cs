using LyuOnnxCore.Extensions;
using LyuOnnxCore.Models;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace LyuOnnxCore.Helpers;

/// <summary>
/// OBB 检测调试辅助类
/// </summary>
public static class ObbDebugHelper
{
    /// <summary>
    /// 调试 OBB 模型输出
    /// </summary>
    public static void DebugObbModel(string modelPath, string imagePath)
    {
        using var session = new InferenceSession(modelPath);
        using var image = Cv2.ImRead(imagePath);

        Console.WriteLine("=== 模型信息 ===");
        Console.WriteLine($"输入名称: {session.InputNames[0]}");
        Console.WriteLine($"输出名称: {session.OutputNames[0]}");

        // 预处理
        var (inputTensor, ratio, padW, padH) = PreprocessImageDebug(image, 640, 640);
        Console.WriteLine($"\n=== 预处理信息 ===");
        Console.WriteLine($"原始图像尺寸: {image.Width}x{image.Height}");
        Console.WriteLine($"缩放比例: {ratio}");
        Console.WriteLine($"填充: padW={padW}, padH={padH}");

        // 推理
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(session.InputNames[0], inputTensor)
        };

        using var outputs = session.Run(inputs);
        var outputTensor = outputs.ElementAt(0).AsTensor<float>();
        var dims = outputTensor.Dimensions.ToArray();

        Console.WriteLine($"\n=== 输出维度 ===");
        Console.WriteLine($"维度: [{string.Join(", ", dims)}]");
        Console.WriteLine($"numFeatures (dims[1]): {dims[1]}");
        Console.WriteLine($"numPredictions (dims[2]): {dims[2]}");

        // 分析前几个预测
        Console.WriteLine($"\n=== 前5个预测分析 ===");
        int numFeatures = dims[1];
        int numPredictions = Math.Min(5, dims[2]);

        for (int i = 0; i < numPredictions; i++)
        {
            Console.WriteLine($"\n预测 #{i}:");
            
            // 打印所有特征值
            for (int f = 0; f < Math.Min(20, numFeatures); f++)
            {
                float value = outputTensor[0, f, i];
                Console.WriteLine($"  特征[{f}]: {value:F6}");
            }
        }

        // 尝试解析（假设格式：4 bbox + classes + 8 corners）
        Console.WriteLine($"\n=== 尝试解析（假设有4个类别） ===");
        int assumedClasses = 4;
        int expectedFeatures = 4 + assumedClasses + 8; // 16
        
        Console.WriteLine($"期望特征数: {expectedFeatures}");
        Console.WriteLine($"实际特征数: {numFeatures}");
        
        if (numFeatures == expectedFeatures)
        {
            Console.WriteLine("✓ 特征数匹配！");
            
            for (int i = 0; i < Math.Min(3, dims[2]); i++)
            {
                Console.WriteLine($"\n预测 #{i}:");
                
                // 类别置信度
                Console.WriteLine("  类别置信度:");
                for (int c = 0; c < assumedClasses; c++)
                {
                    float conf = outputTensor[0, 4 + c, i];
                    Console.WriteLine($"    类别{c}: {conf:F4}");
                }
                
                // 角点坐标
                Console.WriteLine("  角点坐标 (归一化):");
                for (int p = 0; p < 4; p++)
                {
                    float x = outputTensor[0, 8 + p * 2, i];
                    float y = outputTensor[0, 8 + p * 2 + 1, i];
                    Console.WriteLine($"    点{p + 1}: ({x:F4}, {y:F4})");
                }
            }
        }
        else
        {
            Console.WriteLine($"✗ 特征数不匹配！可能的类别数: {numFeatures - 12}");
        }
    }

    private static (Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>, float, int, int) PreprocessImageDebug(
        Mat image, int targetWidth, int targetHeight)
    {
        float ratio = Math.Min((float)targetWidth / image.Width, (float)targetHeight / image.Height);
        int newWidth = (int)(image.Width * ratio);
        int newHeight = (int)(image.Height * ratio);
        int padW = (targetWidth - newWidth) / 2;
        int padH = (targetHeight - newHeight) / 2;

        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(newWidth, newHeight), interpolation: InterpolationFlags.Linear);

        using var padded = new Mat(targetHeight, targetWidth, MatType.CV_8UC3, new Scalar(114, 114, 114));
        var roi = new Rect(padW, padH, newWidth, newHeight);
        resized.CopyTo(new Mat(padded, roi));

        using var rgb = new Mat();
        Cv2.CvtColor(padded, rgb, ColorConversionCodes.BGR2RGB);

        var tensor = new Microsoft.ML.OnnxRuntime.Tensors.DenseTensor<float>([1, 3, targetHeight, targetWidth]);

        unsafe
        {
            byte* ptr = (byte*)rgb.DataPointer;
            int channels = rgb.Channels();

            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    int idx = (y * targetWidth + x) * channels;
                    tensor[0, 0, y, x] = ptr[idx + 0] / 255f;
                    tensor[0, 1, y, x] = ptr[idx + 1] / 255f;
                    tensor[0, 2, y, x] = ptr[idx + 2] / 255f;
                }
            }
        }

        return (tensor, ratio, padW, padH);
    }
}
