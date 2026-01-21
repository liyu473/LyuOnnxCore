using LyuOnnxCore.Extensions;
using OpenCvSharp;
using System.Globalization;
using System.Windows.Data;

namespace MahTemp.Converters;

public class MatToBitmapSourceConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Mat mat || mat.Empty() || mat.IsDisposed)
            return null;

        return mat.ToBitmapSource();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
