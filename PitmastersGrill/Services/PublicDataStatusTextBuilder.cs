using PitmastersGrill.Models;
using System;

namespace PitmastersGrill.Services
{
    public static class PublicDataStatusTextBuilder
    {
        public static string BuildIntelCurrentUpdateStatusText(IntelUpdateStatusSnapshot? snapshot)
        {
            if (snapshot == null)
            {
                return "Public-data status is unknown.";
            }

            if (snapshot.HasError)
            {
                var errorText = string.IsNullOrWhiteSpace(snapshot.ErrorText)
                    ? "provider failure was reported without details"
                    : snapshot.ErrorText.Trim();

                return $"Public-data provider check failed. PMG cannot confirm current freshness right now: {errorText}";
            }

            if (snapshot.IsRunning)
            {
                if (snapshot.IsForegroundPriorityActive)
                {
                    return "Public-data update is paused for foreground activity.";
                }

                return string.IsNullOrWhiteSpace(snapshot.CurrentImportDayUtc)
                    ? "Public-data update is running."
                    : $"Public-data update is running for archive day {snapshot.CurrentImportDayUtc}.";
            }

            if (snapshot.HasRequestedCoverageWindow && snapshot.RequestedCoverageDays > 0)
            {
                if (!snapshot.IsRequestedCoverageComplete)
                {
                    var missingText = snapshot.MissingDayCount > 0
                        ? $"; missing {snapshot.MissingDayCount} day(s)"
                        : string.Empty;

                    return $"Public data is partially populated. Local coverage is {snapshot.LocalCoverageDays} of {snapshot.RequestedCoverageDays} requested archive day(s){missingText}.";
                }

                return $"Public data covers the requested {snapshot.RequestedCoverageDays} archive day(s).";
            }

            if (snapshot.IsCurrentThroughYesterday)
            {
                var throughText = string.IsNullOrWhiteSpace(snapshot.RequiredThroughDayUtc)
                    ? "the latest completed public archive day"
                    : $"completed archive day {snapshot.RequiredThroughDayUtc}";

                return $"Public data archive is current through {throughText}.";
            }

            if (snapshot.MissingDayCount > 0)
            {
                return $"Public data archive is incomplete. Missing {snapshot.MissingDayCount} archive day(s); PMG may show stale or partial public evidence until catch-up completes.";
            }

            if (!string.IsNullOrWhiteSpace(snapshot.LatestCompleteDayUtc))
            {
                return $"Public data archive is idle. Latest complete local archive day is {snapshot.LatestCompleteDayUtc}.";
            }

            return "Public-data status is idle. No local archive coverage has been recorded yet.";
        }
    }
}
