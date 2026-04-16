using System.IO;

namespace PitmastersGrill.Persistence
{
    public static class DebugPaths
    {
        public static string GetDebugDirectory()
        {
            var debugPath = Path.Combine(AppPaths.GetAppDataDirectory(), "debug");

            Directory.CreateDirectory(debugPath);

            return debugPath;
        }

        public static string GetResolverTracePath()
        {
            return Path.Combine(GetDebugDirectory(), "resolver_trace.txt");
        }

        public static string GetEveWhoCharacterJsonPath()
        {
            return Path.Combine(GetDebugDirectory(), "evewho_character_sample.json");
        }

        public static string GetDotlanSearchHtmlPath()
        {
            return Path.Combine(GetDebugDirectory(), "dotlan_search_sample.html");
        }

        public static string GetDotlanCorporationHtmlPath()
        {
            return Path.Combine(GetDebugDirectory(), "dotlan_corporation_sample.html");
        }

        public static string GetDotlanAllianceHtmlPath()
        {
            return Path.Combine(GetDebugDirectory(), "dotlan_alliance_sample.html");
        }

        public static string GetZkillSearchHtmlPath()
        {
            return Path.Combine(GetDebugDirectory(), "zkill_search_sample.html");
        }
    }
}