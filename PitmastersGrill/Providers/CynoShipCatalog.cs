using System.Collections.Generic;

namespace PitmastersGrill.Providers
{
    public class CynoShipCatalog
    {
        private static readonly Dictionary<int, string> CynoShipTypeNames = new()
        {
            // Force Recons
            { 11957, "Falcon" },
            { 11963, "Rapier" },
            { 11965, "Pilgrim" },
            { 11969, "Arazu" },

            // Black Ops / special black ops
            { 22428, "Redeemer" },
            { 22430, "Sin" },
            { 22436, "Widow" },
            { 22440, "Panther" },
            { 44996, "Marshal" },
            { 44995, "Enforcer" },
            { 85236, "Python" },

            // Heavy Interdiction Cruisers
            { 11995, "Onyx" },
            { 12013, "Broadsword" },
            { 12017, "Devoter" },
            { 12021, "Phobos" },
            { 60764, "Laelaps" },

            // Stealth Bombers / special covert
            { 11377, "Nemesis" },
            { 12032, "Manticore" },
            { 12034, "Hound" },
            { 12038, "Purifier" },
            { 44993, "Pacifier" },

            // Industrial cyno - Tech I Industrials
            { 648, "Badger" },
            { 649, "Tayra" },
            { 650, "Nereus" },
            { 651, "Hoarder" },
            { 652, "Mammoth" },
            { 653, "Wreathe" },
            { 654, "Kryos" },
            { 655, "Epithal" },
            { 656, "Miasmos" },
            { 657, "Iteron Mark V" },
            { 1944, "Bestower" },
            { 19744, "Sigil" },

            // Industrial cyno - Blockade Runners / Deep Space Transports
            { 12729, "Crane" },
            { 12731, "Bustard" },
            { 12733, "Prorator" },
            { 12735, "Prowler" },
            { 12743, "Viator" },
            { 12745, "Occator" },
            { 12747, "Mastodon" },
            { 12753, "Impel" },

            // User-requested T3 cruisers
            { 29984, "Tengu" },
            { 29986, "Legion" },
            { 29988, "Proteus" },
            { 29990, "Loki" }
        };

        public bool TryGetCynoShipName(int? shipTypeId, out string shipName)
        {
            shipName = "";

            if (!shipTypeId.HasValue || shipTypeId.Value <= 0)
            {
                return false;
            }

            return CynoShipTypeNames.TryGetValue(shipTypeId.Value, out shipName!);
        }
    }
}
