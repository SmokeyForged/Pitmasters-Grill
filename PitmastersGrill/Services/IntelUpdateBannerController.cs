using PitmastersGrill.Models;
using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PitmastersGrill.Services
{
    public sealed class IntelUpdateBannerController
    {
        private static readonly SolidColorBrush CurrentBrush = CreateFrozenBrush("#155724");
        private static readonly SolidColorBrush StaleOrRunningBrush = CreateFrozenBrush("#5A1111");
        private static readonly SolidColorBrush ErrorBrush = CreateFrozenBrush("#6E1111");

        private readonly Dispatcher _dispatcher;

        public IntelUpdateBannerController(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void HandleStatusChanged(
            IntelUpdateStatusSnapshot snapshot,
            Border banner,
            TextBlock statusText,
            TextBlock detailText)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.Invoke(() => ApplySnapshot(snapshot, banner, statusText, detailText));
                return;
            }

            ApplySnapshot(snapshot, banner, statusText, detailText);
        }

        public void ApplySnapshot(
            IntelUpdateStatusSnapshot snapshot,
            Border banner,
            TextBlock statusText,
            TextBlock detailText)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (banner == null)
            {
                throw new ArgumentNullException(nameof(banner));
            }

            if (statusText == null)
            {
                throw new ArgumentNullException(nameof(statusText));
            }

            if (detailText == null)
            {
                throw new ArgumentNullException(nameof(detailText));
            }

            statusText.Text = NormalizeKillmailIntelText(snapshot.StatusText);
            detailText.Text = NormalizeKillmailIntelText(snapshot.DetailText);

            if (snapshot.HasError)
            {
                banner.Background = ErrorBrush;
                return;
            }

            if (snapshot.IsRunning || !snapshot.IsCurrentThroughYesterday)
            {
                banner.Background = StaleOrRunningBrush;
                return;
            }

            banner.Background = CurrentBrush;
        }

        private static string NormalizeKillmailIntelText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input ?? string.Empty;
            }

            var normalized = input
                .Replace("LOCAL INTEL", "KILLMAIL INTEL", StringComparison.OrdinalIgnoreCase)
                .Replace("Local intel", "Killmail intel", StringComparison.OrdinalIgnoreCase);

            if (normalized.Contains("KILLMAIL INTEL CURRENT", StringComparison.OrdinalIgnoreCase))
            {
                return "Intel current";
            }

            if (normalized.Contains("Current through latest published archive", StringComparison.OrdinalIgnoreCase))
            {
                return "Through latest archive";
            }

            var throughIndex = normalized.IndexOf(" through ", StringComparison.OrdinalIgnoreCase);
            if (throughIndex >= 0)
            {
                var date = TryGetLastIsoDate(normalized);
                if (!string.IsNullOrWhiteSpace(date))
                {
                    return $"Through {date}";
                }
            }

            return normalized;
        }

        private static string TryGetLastIsoDate(string input)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(input ?? "", @"\d{4}-\d{2}-\d{2}");
            return matches.Count == 0
                ? ""
                : matches[matches.Count - 1].Value;
        }

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}
