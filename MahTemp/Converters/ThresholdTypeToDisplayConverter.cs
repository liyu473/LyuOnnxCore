using System.Globalization;
using System.Windows.Data;
using OpenCvSharp;

namespace MahTemp.Converters;

/// <summary>
/// 将二值化类型转换为中英文显示
/// </summary>
public class ThresholdTypeToDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ThresholdTypes type)
        {
            return type switch
            {
                ThresholdTypes.Binary => "Binary - 二值化",
                ThresholdTypes.BinaryInv => "BinaryInv - 反二值化",
                ThresholdTypes.Trunc => "Trunc - 截断",
                ThresholdTypes.Tozero => "Tozero - 阈值化为零",
                ThresholdTypes.TozeroInv => "TozeroInv - 反阈值化为零",
                ThresholdTypes.Otsu => "Otsu - 大津法(自动)",
                ThresholdTypes.Triangle => "Triangle - 三角法(自动)",
                _ => value.ToString() ?? string.Empty
            };
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
