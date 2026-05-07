using PitmastersGrill.Models;
using System;

namespace PitmastersGrill.Services
{
    public sealed class DiagnosticsCacheStatsPresenter
    {
        public string BuildStatsText(CacheStatsSnapshot stats)
        {
            if (stats == null)
            {
                throw new ArgumentNullException(nameof(stats));
            }

            return CacheMaintenanceService.FormatStats(stats);
        }

        public string BuildFailureText(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return $"Cache stats failed: {exception.Message}";
        }
    }
}
