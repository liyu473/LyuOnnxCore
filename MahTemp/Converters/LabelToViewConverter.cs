using System.Globalization;
using System.Windows.Data;

namespace MahTemp.Converters;

public class LabelToViewConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string label)
            return null;

        return label switch
        {
            "Home" => App.GetService<Views.HomePage>(),
            "Settings" => App.GetService<Views.SettingsPage>(),
            "Detection" => App.GetService<Views.DetectionPage>(),
            _ => null
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
