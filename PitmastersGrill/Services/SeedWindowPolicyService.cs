using System;
using System.Globalization;

namespace PitmastersGrill.Services
{
    public class SeedWindowPolicyService
    {
        public SeedWindowResult GetSixMonthSeedWindowUtc()
        {
            var todayUtc = DateTime.UtcNow.Date;
            var endDay = todayUtc.AddDays(-1);
            var startDay = endDay.AddMonths(-6).AddDays(1);

            return new SeedWindowResult
            {
                StartDayUtc = startDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EndDayUtc = endDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DayCount = (endDay - startDay).Days + 1
            };
        }

        public SeedWindowResult GetExplicitWindowUtc(string startDayUtc, string endDayUtc)
        {
            if (!TryParseDay(startDayUtc, out var startDay))
            {
                throw new InvalidOperationException($"Invalid start day: {startDayUtc}");
            }

            if (!TryParseDay(endDayUtc, out var endDay))
            {
                throw new InvalidOperationException($"Invalid end day: {endDayUtc}");
            }

            if (endDay < startDay)
            {
                throw new InvalidOperationException($"End day {endDayUtc} is before start day {startDayUtc}.");
            }

            return new SeedWindowResult
            {
                StartDayUtc = startDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                EndDayUtc = endDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DayCount = (endDay - startDay).Days + 1
            };
        }

        private static bool TryParseDay(string dayUtc, out DateTime parsedDay)
        {
            return DateTime.TryParseExact(
                dayUtc,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedDay);
        }
    }

    public class SeedWindowResult
    {
        public string StartDayUtc { get; set; } = "";
        public string EndDayUtc { get; set; } = "";
        public int DayCount { get; set; }
    }
}