using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PitmastersGrill.Services
{
    public enum BoardPopulationStatusKind
    {
        Neutral,
        Success,
        Warning,
        Error
    }

    public sealed class BoardPopulationStatusController
    {
        private BoardPopulationStatusKind _currentKind = BoardPopulationStatusKind.Neutral;

        public BoardPopulationStatusKind CurrentKind => _currentKind;

        public void UpdateStatus(
            string statusText,
            BoardPopulationStatusKind kind,
            TextBlock statusTextBlock,
            ResourceDictionary resources)
        {
            if (statusTextBlock == null)
            {
                throw new ArgumentNullException(nameof(statusTextBlock));
            }

            statusTextBlock.Text = statusText ?? string.Empty;
            _currentKind = kind;
            ApplyStatusVisual(statusTextBlock, resources);
        }

        public void ApplyStatusVisual(TextBlock statusTextBlock, ResourceDictionary resources)
        {
            if (statusTextBlock == null)
            {
                throw new ArgumentNullException(nameof(statusTextBlock));
            }

            statusTextBlock.Foreground = ResolveBrush(resources, _currentKind);
        }

        private static Brush ResolveBrush(ResourceDictionary resources, BoardPopulationStatusKind kind)
        {
            Brush brush = resources?["MutedTextBrush"] as Brush ?? Brushes.LightGray;

            switch (kind)
            {
                case BoardPopulationStatusKind.Success:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
                case BoardPopulationStatusKind.Warning:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
                case BoardPopulationStatusKind.Error:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
                default:
                    return brush;
            }
        }
    }
}
