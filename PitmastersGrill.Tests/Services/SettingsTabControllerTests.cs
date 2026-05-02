using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public class SettingsTabControllerTests
    {
        [Fact]
        public void ApplySettingsToControls_MapsSettingsToLiveFeedRepairAndPlacementControls()
        {
            RunOnStaThread(() =>
            {
                var controller = new SettingsTabController();
                var settings = new AppSettings
                {
                    LiveZkillFeedEnabled = true,
                    BackgroundHistoricalRepairEnabled = false,
                    PilotDetailPlacementPreference = PilotDetailPlacementPreference.AutoPreferLeft.ToString()
                };

                var liveFeed = new CheckBox();
                var backgroundRepair = new CheckBox();
                var placement = CreatePilotDetailPlacementComboBox();

                controller.ApplySettingsToControls(settings, liveFeed, backgroundRepair, placement);

                Assert.True(liveFeed.IsChecked);
                Assert.False(backgroundRepair.IsChecked);
                Assert.Equal(1, placement.SelectedIndex);
            });
        }

        [Fact]
        public void ApplySettingsToControls_FallsBackToRightPlacementForInvalidSavedValue()
        {
            RunOnStaThread(() =>
            {
                var controller = new SettingsTabController();
                var settings = new AppSettings
                {
                    PilotDetailPlacementPreference = "NotARealPreference"
                };
                var placement = CreatePilotDetailPlacementComboBox();

                controller.ApplySettingsToControls(settings, null, null, placement);

                Assert.Equal(0, placement.SelectedIndex);
            });
        }

        [Theory]
        [InlineData(PilotDetailPlacementPreference.AutoPreferRight, 0)]
        [InlineData(PilotDetailPlacementPreference.AutoPreferLeft, 1)]
        public void GetPilotDetailPlacementPreference_ParsesKnownValues(
            PilotDetailPlacementPreference expected,
            int selectedIndex)
        {
            var controller = new SettingsTabController();
            var settings = new AppSettings
            {
                PilotDetailPlacementPreference = expected.ToString()
            };

            controller.SetPilotDetailPlacementPreference(settings, selectedIndex);
            var result = controller.GetPilotDetailPlacementPreference(settings);

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData(0, "AutoPreferRight")]
        [InlineData(1, "AutoPreferLeft")]
        [InlineData(99, "AutoPreferRight")]
        public void SetPilotDetailPlacementPreference_MapsSelectedIndexToSavedValue(int selectedIndex, string expected)
        {
            var controller = new SettingsTabController();
            var settings = new AppSettings();

            controller.SetPilotDetailPlacementPreference(settings, selectedIndex);

            Assert.Equal(expected, settings.PilotDetailPlacementPreference);
        }

        [Fact]
        public void LiveFeedAndBackgroundRepairSetters_UpdateSettings()
        {
            var controller = new SettingsTabController();
            var settings = new AppSettings();

            controller.SetLiveZkillFeedEnabled(settings, true);
            controller.SetBackgroundHistoricalRepairEnabled(settings, false);

            Assert.True(settings.LiveZkillFeedEnabled);
            Assert.False(settings.BackgroundHistoricalRepairEnabled);
        }

        private static ComboBox CreatePilotDetailPlacementComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Auto Prefer Right");
            comboBox.Items.Add("Auto Prefer Left");
            return comboBox;
        }

        private static void RunOnStaThread(Action action)
        {
            Exception? capturedException = null;

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (capturedException != null)
            {
                ExceptionDispatchInfo.Capture(capturedException).Throw();
            }
        }
    }
}
