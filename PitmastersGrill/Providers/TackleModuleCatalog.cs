using PitmastersGrill.Models;
using System;
using System.Collections.Generic;

namespace PitmastersGrill.Providers
{
    public static class TackleModuleCatalog
    {
        private static readonly IReadOnlyDictionary<int, TackleModuleCatalogEntry> KnownTackleModules =
            new Dictionary<int, TackleModuleCatalogEntry>
            {
                // Warp Scramblers - regular variants.
                [447] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Warp Scrambler I"),
                [5439] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "J5b Enduring Warp Scrambler"),
                [5443] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Faint Epsilon Scoped Warp Scrambler"),
                [5445] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Initiated Compact Warp Scrambler"),
                [448] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Warp Scrambler II"),
                [21512] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "'Delineative' Warp Scrambler"),
                [14252] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Dark Blood Warp Scrambler"),
                [14254] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Domination Warp Scrambler"),
                [14256] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Dread Guristas Warp Scrambler"),
                [14258] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "True Sansha Warp Scrambler"),
                [14260] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Shadow Serpentis Warp Scrambler"),
                [15887] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Caldari Navy Warp Scrambler"),
                [15893] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Republic Fleet Warp Scrambler"),
                [28518] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Khanid Navy Warp Scrambler"),
                [41061] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Federation Navy Warp Scrambler"),
                [47732] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Abyssal Warp Scrambler"),

                // Warp Scramblers - heavy variants.
                [40750] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Heavy Warp Scrambler I"),
                [40752] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Heavy Initiated Compact Warp Scrambler"),
                [40754] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Heavy J5b Enduring Warp Scrambler"),
                [40756] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Heavy Faint Epsilon Scoped Warp Scrambler"),
                [40758] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Heavy Warp Scrambler II"),
                [40762] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Shadow Serpentis Heavy Warp Scrambler"),
                [40764] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Domination Heavy Warp Scrambler"),
                [14664] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Mizuro's Modified Heavy Warp Scrambler"),
                [14666] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Hakim's Modified Heavy Warp Scrambler"),
                [14668] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Gotan's Modified Heavy Warp Scrambler"),
                [14670] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Tobias' Modified Heavy Warp Scrambler"),
                [56303] = new TackleModuleCatalogEntry(TackleModuleType.WarpScrambler, "Heavy Abyssal Warp Scrambler"),

                // Warp Disruptors - regular variants.
                [3242] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Warp Disruptor I"),
                [5399] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "J5 Enduring Warp Disruptor"),
                [5403] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Faint Scoped Warp Disruptor"),
                [5405] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Initiated Compact Warp Disruptor"),
                [3244] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Warp Disruptor II"),
                [21510] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "'Interruptive' Warp Disruptor"),
                [14242] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Dark Blood Warp Disruptor"),
                [14244] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Domination Warp Disruptor"),
                [14246] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Dread Guristas Warp Disruptor"),
                [14248] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "True Sansha Warp Disruptor"),
                [14250] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Shadow Serpentis Warp Disruptor"),
                [15889] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Caldari Navy Warp Disruptor"),
                [15891] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Republic Fleet Warp Disruptor"),
                [28516] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Khanid Navy Warp Disruptor"),
                [41062] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Federation Navy Warp Disruptor"),
                [47736] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Abyssal Warp Disruptor"),

                // Warp Disruptors - heavy variants.
                [40730] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Heavy Warp Disruptor I"),
                [40731] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Heavy Fleeting Compact Warp Disruptor"),
                [40732] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Heavy J5 Enduring Warp Disruptor"),
                [40733] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Heavy Faint Scoped Warp Disruptor"),
                [40734] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Heavy Warp Disruptor II"),
                [40736] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Shadow Serpentis Heavy Warp Disruptor"),
                [40737] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Domination Heavy Warp Disruptor"),
                [14656] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Mizuro's Modified Heavy Warp Disruptor"),
                [14658] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Hakim's Modified Heavy Warp Disruptor"),
                [14660] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Gotan's Modified Heavy Warp Disruptor"),
                [14662] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Tobias' Modified Heavy Warp Disruptor"),
                [56304] = new TackleModuleCatalogEntry(TackleModuleType.WarpDisruptor, "Heavy Abyssal Warp Disruptor")
            };

        public static bool TryGetKnownTackleModule(int typeId, out TackleModuleType tackleType, out string moduleName)
        {
            if (KnownTackleModules.TryGetValue(typeId, out var entry))
            {
                tackleType = entry.TackleType;
                moduleName = entry.ModuleName;
                return true;
            }

            tackleType = TackleModuleType.UnknownTackle;
            moduleName = string.Empty;
            return false;
        }

        private sealed class TackleModuleCatalogEntry
        {
            public TackleModuleCatalogEntry(TackleModuleType tackleType, string moduleName)
            {
                TackleType = tackleType;
                ModuleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
            }

            public TackleModuleType TackleType { get; }

            public string ModuleName { get; }
        }
    }
}
