using Microsoft.UI.Xaml.Data;

namespace Trace.Presentation.Controls;

/// <summary>
/// MVUX binds through the classic pipeline, which has no implicit bool-to-Visibility
/// conversion, so the app carries its own. <c>Invert</c> as the parameter flips it.
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var visible = value is bool flag && flag;

        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is Visibility.Visible;
}

/// <summary>Shows content only when a string has something in it.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var present = !string.IsNullOrWhiteSpace(value as string);

        if (string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase))
        {
            present = !present;
        }

        return present ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>
/// Maps any of the domain's status enums onto the one severity scale the UI paints with.
/// Keeping the mapping here means the domain never has to know about colour.
/// </summary>
public sealed class SeverityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        Disposition disposition => disposition.ToSeverity(),
        AssetCondition condition => condition.ToSeverity(),
        RangeStatus range => range.ToSeverity(),
        SyncState state => state.ToSeverity(),
        CorrectivePriority priority => priority.ToSeverity(),
        _ => StatusSeverity.Neutral,
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Renders a timestamp the way a technician reads a round: relative, then exact.</summary>
/// <remarks>
/// XAML creates converters outside dependency injection, so this one reads the system clock;
/// the formatting itself is <see cref="Format"/>, which takes the time to measure from.
/// </remarks>
public sealed class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is DateTimeOffset at ? Format(at, TimeProvider.System.GetLocalNow()) : "Never";

    /// <summary>
    /// Relative within the week, then the date; the year is added once the date is not in
    /// the current one, so an install date from 2019 does not read as this spring.
    /// </summary>
    public static string Format(DateTimeOffset at, DateTimeOffset now)
    {
        var elapsed = now - at;

        return elapsed switch
        {
            { TotalMinutes: < 1 } => "Just now",
            { TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes} min ago",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours} h ago",
            { TotalDays: < 7 } => $"{(int)elapsed.TotalDays} d ago",
            _ when at.Year == now.Year => at.ToString("d MMM"),
            _ => at.ToString("d MMM yyyy"),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
