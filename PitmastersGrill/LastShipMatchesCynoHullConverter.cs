using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace PitmastersLittleGrill
{
    public class LastShipMatchesCynoHullConverter : IMultiValueConverter
    {
        private static readonly HashSet<string> ApprovedRedHighlightCynoShips =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Arazu",
                "Pilgrim",
                "Falcon",
                "Rapier",
                "Proteus",
                "Legion",
                "Loki",
                "Tengu",
                "Hound",
                "Purifier",
                "Manticore",
                "Nemesis",
                "Cheetah",
                "Anathema",
                "Buzzard",
                "Helios",
                "Pacifier",
                "Broadsword",
                "Devoter",
                "Onyx",
                "Phobos"
            };

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

            if (string.IsNullOrWhiteSpace(lastShipSeen) || string.IsNullOrWhiteSpace(cynoHullSeen))
            {
                return false;
            }

            return ApprovedRedHighlightCynoShips.Contains(lastShipSeen.Trim()) &&
                   ApprovedRedHighlightCynoShips.Contains(cynoHullSeen.Trim());
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}