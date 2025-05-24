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

[ValueConversion(typeof(Enum), typeof(bool))]
public class RadioButtonEnumConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        return value.ToString() == parameter.ToString();
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return Binding.DoNothing;
        if ((bool)value)
        {
            return Enum.Parse(targetType, parameter.ToString());
        }
        return Binding.DoNothing;
    }
}
