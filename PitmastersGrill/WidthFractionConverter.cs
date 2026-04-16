using System;
using System.Globalization;
using System.Windows.Data;

namespace PitmastersLittleGrill
{
    public class WidthFractionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double width)
            {
                return 300.0;
            }

            var fraction = 0.33;

            if (parameter is string parameterText &&
                double.TryParse(parameterText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFraction) &&
                parsedFraction > 0)
            {
                fraction = parsedFraction;
            }

            return width * fraction;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}