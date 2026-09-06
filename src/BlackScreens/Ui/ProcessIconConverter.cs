using System.Globalization;
using System.Windows.Data;

namespace BlackScreens.Ui;

/// <summary>Binds a program list row to that program's icon.</summary>
internal sealed class ProcessIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ProcessIconProvider.For(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// What a program list row reads as. An entry naming one exact executable is shown by its file name,
/// because the whole path is too long for the row; the row's tooltip carries the full thing.
/// </summary>
internal sealed class ProcessLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string entry ? ProcessRules.Display(entry) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// The quieter half of a program list row: the folder for an entry that names one exact executable,
/// or "any copy" for one that names a program.
/// </summary>
internal sealed class ProcessDetailConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string entry ? ProcessRules.Detail(entry) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
