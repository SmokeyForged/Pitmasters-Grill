using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using PitmastersGrill.Providers;

namespace PitmastersGrill
{
    public class ThreatCynoHullHighlightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return CynoShipCatalog.IsKnownCynoShipName(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
