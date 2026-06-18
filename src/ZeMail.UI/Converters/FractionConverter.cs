using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace ZeMail.UI.Converters;

/// <summary>
/// Multipliziert einen double-Wert (0.0–1.0) mit dem ConverterParameter (Gesamtbreite)
/// und gibt die absolute Pixel-Zahl zurück.
/// </summary>
public class FractionToPixelConverter : IValueConverter
{
    public static readonly FractionToPixelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double fraction && parameter is string p && double.TryParse(p, NumberStyles.Any, CultureInfo.InvariantCulture, out double total))
            return fraction * total;
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}