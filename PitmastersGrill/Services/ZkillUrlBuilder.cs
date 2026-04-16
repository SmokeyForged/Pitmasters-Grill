using System;

namespace PitmastersGrill.Services
{
    public class ZkillUrlBuilder
    {
        public string BuildCharacterUrl(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return string.Empty;
            }

            return $"https://zkillboard.com/character/{Uri.EscapeDataString(characterId)}/";
        }

        public string BuildSearchUrl(string characterName)
        {
            if (string.IsNullOrWhiteSpace(characterName))
            {
                return "https://zkillboard.com/";
            }

            return $"https://zkillboard.com/search/{Uri.EscapeDataString(characterName)}/";
        }
    }
}