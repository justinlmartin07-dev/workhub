using System.Globalization;

namespace WorkHub.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}

public class StatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "New" => Color.FromArgb("#3B82F6"),          // Info blue
            "In Progress" => Color.FromArgb("#F59E0B"),  // Warning amber
            "On Hold" => Color.FromArgb("#94A3B8"),      // Slate gray
            "Complete" => Color.FromArgb("#10B981"),      // Success green
            "Billed" => Color.FromArgb("#8B5CF6"),        // Violet
            "Cancelled" => Color.FromArgb("#EF4444"),     // Danger red
            _ => Color.FromArgb("#94A3B8")                // Slate gray
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class PriorityColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "High" => Color.FromArgb("#EF4444"),     // Danger red
            "Medium" => Color.FromArgb("#F59E0B"),   // Warning amber
            "Low" => Color.FromArgb("#10B981"),      // Success green
            _ => Color.FromArgb("#94A3B8")           // Slate gray
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class StringNotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrWhiteSpace(value?.ToString());

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class IsNotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class DateFormatConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
        {
            var format = parameter?.ToString() ?? "MMM dd, yyyy";
            return dt.ToLocalTime().ToString(format);
        }
        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class LabelCaseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value?.ToString();
        if (string.IsNullOrEmpty(s)) return s;
        return culture.TextInfo.ToTitleCase(s.Replace('_', ' ').ToLower());
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class IndexEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int idx && parameter is string s && int.TryParse(s, out var p))
            return idx == p;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}