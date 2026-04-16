namespace PitmastersLittleGrill.Models
{
    public class PilotFleetObservationDayRecord
    {
        public string DayUtc { get; set; } = "";
        public string CharacterId { get; set; } = "";
        public int AttackerSampleCount { get; set; }
        public int AttackerCountSum { get; set; }
        public string DerivedAtUtc { get; set; } = "";
    }
}