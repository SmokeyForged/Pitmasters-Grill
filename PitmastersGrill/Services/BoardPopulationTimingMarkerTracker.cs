using PitmastersGrill.Diagnostics;
using PitmastersGrill.Models;
using PitmastersGrill.Persistence;

namespace PitmastersGrill.Services
{
    public sealed class BoardPopulationTimingMarkerTracker
    {
        private readonly object _sync = new();
        private int _firstResolverLoggedGeneration = -1;
        private int _firstIdentityUiLoggedGeneration = -1;
        private int _firstAffiliationLoggedGeneration = -1;
        private int _firstStatsLoggedGeneration = -1;
        private int _firstIgnoredAllianceSkipLoggedGeneration = -1;

        public void HandleMarker(BoardRowProcessMarkerKind markerKind, int generation, string message)
        {
            switch (markerKind)
            {
                case BoardRowProcessMarkerKind.ResolverValueAvailable:
                    TryWriteMarker(ref _firstResolverLoggedGeneration, generation, message);
                    break;

                case BoardRowProcessMarkerKind.IdentityUiUpdated:
                    TryWriteMarker(ref _firstIdentityUiLoggedGeneration, generation, message);
                    break;

                case BoardRowProcessMarkerKind.AffiliationUiUpdated:
                    TryWriteMarker(ref _firstAffiliationLoggedGeneration, generation, message);
                    break;

                case BoardRowProcessMarkerKind.StatsUiUpdated:
                    TryWriteMarker(ref _firstStatsLoggedGeneration, generation, message);
                    break;

                case BoardRowProcessMarkerKind.IgnoredAllianceStatsSkipped:
                    TryWriteMarker(ref _firstIgnoredAllianceSkipLoggedGeneration, generation, message);
                    break;
            }
        }

        private void TryWriteMarker(ref int markerField, int generation, string message)
        {
            lock (_sync)
            {
                if (markerField == generation)
                {
                    return;
                }

                markerField = generation;
                DebugTraceWriter.WriteLine(message);
            }
        }
    }
}
