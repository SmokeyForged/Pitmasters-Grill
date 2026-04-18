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

            return input
                .Replace("LOCAL INTEL", "KILLMAIL INTEL", StringComparison.OrdinalIgnoreCase)
                .Replace("Local intel", "Killmail intel", StringComparison.OrdinalIgnoreCase);
        }

        private static SolidColorBrush CreateFrozenBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }
    }
}