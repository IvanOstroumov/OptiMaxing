using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using OptiMaxing.Core.Model;

namespace OptiMaxing.App;

public sealed class RiskToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is RiskLevel risk
            ? Application.Current.FindResource(risk switch
            {
                RiskLevel.Safe => "RiskSafeBrush",
                RiskLevel.Caution => "RiskCautionBrush",
                RiskLevel.Advanced => "RiskAdvancedBrush",
                _ => "RiskAdvisoryBrush",
            })
            : Brushes.Gray;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
