using System.Globalization;
using System.Windows.Data;
using OpenCvSharp;

namespace MahTemp.Converters;

/// <summary>
/// 根据二值化类型判断阈值参数是否可用
/// Otsu 和 Triangle 会自动计算阈值，所以阈值参数应该禁用
/// </summary>
public class ThresholdTypeToEnabledConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ThresholdTypes type)
        {
            // Otsu 和 Triangle 会自动计算阈值，所以阈值参数应该禁用
            return type != ThresholdTypes.Otsu && type != ThresholdTypes.Triangle;
        }
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
