using System;
using System.Globalization;

namespace PitmastersGrill.Services
{
    public static class LastSeenDisplayFormatter
    {
        public static string FormatLastSeen(string utcValue, DateTime? nowUtc = null)
        {
            if (string.IsNullOrWhiteSpace(utcValue))
            {
                return string.Empty;
            }

            if (!DateTime.TryParse(
                    utcValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsedUtc))
            {
                return string.Empty;
            }

            var effectiveNowUtc = nowUtc ?? DateTime.UtcNow;
            if (parsedUtc > effectiveNowUtc)
            {
                parsedUtc = effectiveNowUtc;
            }

            var elapsed = effectiveNowUtc - parsedUtc;
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            if (parsedUtc.Date == effectiveNowUtc.Date)
            {
                if (elapsed.TotalMinutes < 60)
                {
                    return elapsed.TotalMinutes < 1
                        ? "now"
                        : $"{Math.Max(1, (int)Math.Round(elapsed.TotalMinutes))}m ago";
                }

                return $"{Math.Max(1, (int)Math.Floor(elapsed.TotalHours))}h ago";
            }

            var days = Math.Max(1, (int)Math.Floor((effectiveNowUtc.Date - parsedUtc.Date).TotalDays));
            return days == 1 ? "1d ago" : $"{days}d ago";
        }
    }
}
