namespace PitmastersGrill.Services
{
    public static class AppReleaseMetadata
    {
        public const string ProductUserAgentName = "PitmastersGrill";
        public const string DisplayName = "Pitmasters Grill";
        public const string ReleaseStage = "General Release";

        public static string VersionText => FormatVersion(typeof(AppReleaseMetadata).Assembly.GetName().Version);

        public static string ReleaseLabel => $"{ReleaseStage}-v{VersionText}";

        public static string GenericUserAgent => $"{ProductUserAgentName}/{VersionText}";

        public static string PanelModeAlwaysEnabledText => $"Panel Mode is always enabled for PMG {VersionText}.";

        private static string FormatVersion(System.Version? version)
        {
            if (version == null)
            {
                return "0.0.0";
            }

            var major = version.Major < 0 ? 0 : version.Major;
            var minor = version.Minor < 0 ? 0 : version.Minor;
            var build = version.Build < 0 ? 0 : version.Build;

            return $"{major}.{minor}.{build}";
        }
    }
}
