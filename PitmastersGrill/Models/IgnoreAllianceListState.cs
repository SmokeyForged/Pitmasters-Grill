using System.Collections.Generic;

namespace PitmastersGrill.Models
{
    public class IgnoreAllianceListState
    {
        public List<long> AllianceIds { get; set; } = new();
        public List<TypedIgnoreEntry> Entries { get; set; } = new();
    }
}
