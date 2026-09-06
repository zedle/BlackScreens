using System.Globalization;
using System.Windows.Data;

namespace BlackScreens.Ui;

/// <summary>Binds a denylist row, which is just a process name, to that program's icon.</summary>
internal sealed class ProcessIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ProcessIconProvider.For(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
