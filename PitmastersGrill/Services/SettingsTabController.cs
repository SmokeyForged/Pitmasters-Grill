using PitmastersGrill.Models;
using System;
using System.Windows.Controls;

namespace PitmastersGrill.Services
{
    public sealed class SettingsTabController
    {
        public void ApplySettingsToControls(
            AppSettings settings,
            CheckBox? enableLiveZkillFeedCheckBox,
            CheckBox? backgroundHistoricalRepairEnabledCheckBox,
            ComboBox? pilotDetailPlacementComboBox)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (enableLiveZkillFeedCheckBox != null)
            {
                enableLiveZkillFeedCheckBox.IsChecked = settings.LiveZkillFeedEnabled;
            }

            if (backgroundHistoricalRepairEnabledCheckBox != null)
            {
                backgroundHistoricalRepairEnabledCheckBox.IsChecked = settings.BackgroundHistoricalRepairEnabled;
            }

            if (pilotDetailPlacementComboBox != null)
            {
                pilotDetailPlacementComboBox.SelectedIndex = GetPilotDetailPlacementPreference(settings) == PilotDetailPlacementPreference.AutoPreferLeft
                    ? 1
                    : 0;
            }
        }

        public PilotDetailPlacementPreference GetPilotDetailPlacementPreference(AppSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return Enum.TryParse<PilotDetailPlacementPreference>(
                settings.PilotDetailPlacementPreference,
                ignoreCase: true,
                out var parsed)
                ? parsed
                : PilotDetailPlacementPreference.AutoPreferRight;
        }

        public void SetPilotDetailPlacementPreference(AppSettings settings, int selectedIndex)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.PilotDetailPlacementPreference = selectedIndex == 1
                ? PilotDetailPlacementPreference.AutoPreferLeft.ToString()
                : PilotDetailPlacementPreference.AutoPreferRight.ToString();
        }

        public void SetLiveZkillFeedEnabled(AppSettings settings, bool enabled)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.LiveZkillFeedEnabled = enabled;
        }

        public void SetBackgroundHistoricalRepairEnabled(AppSettings settings, bool enabled)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.BackgroundHistoricalRepairEnabled = enabled;
        }
    }
}
