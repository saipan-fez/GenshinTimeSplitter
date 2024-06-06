using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace GenshinTimeSplitter.Views;

public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (value is null || parameter is null)
                return false;

            var types = parameter.ToString().Split(',');
            return types.Contains(value.ToString());
        }
        catch
        {
            return false;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
