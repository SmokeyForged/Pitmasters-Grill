using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using PitmastersGrill.Providers;

namespace PitmastersGrill
{
    public class LastShipMatchesCynoHullConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3)
            {
                return false;
            }

            var lastShipSeen = values[0] as string;
            var cynoHullSeen = values[1] as string;

            var knownCynoOverride = false;
            if (values[2] is bool boolValue)
            {
                knownCynoOverride = boolValue;
            }

            if (knownCynoOverride)
            {
                return true;
            }

            return CynoShipCatalog.IsKnownCynoShipName(lastShipSeen) &&
                   CynoShipCatalog.IsKnownCynoShipName(cynoHullSeen);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
