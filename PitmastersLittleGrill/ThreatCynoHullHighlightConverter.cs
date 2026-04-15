using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace PitmastersLittleGrill
{
    public class ThreatCynoHullHighlightConverter : IValueConverter
    {
        private static readonly HashSet<string> ApprovedOrangeHighlightCynoShips =
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

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var hullName = value as string;

            if (string.IsNullOrWhiteSpace(hullName))
            {
                return false;
            }

            return ApprovedOrangeHighlightCynoShips.Contains(hullName.Trim());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}