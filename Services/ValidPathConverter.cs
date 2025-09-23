using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace VManager.Services;

public class ValidPathConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path)
        {
            bool isValid = !string.IsNullOrEmpty(path);
            Console.WriteLine($"[DEBUG]: ValidPathConverter - path: '{path}', isValid: {isValid}");
            return isValid;
        }
        Console.WriteLine("[DEBUG]: ValidPathConverter - value is not a string or is null");
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}