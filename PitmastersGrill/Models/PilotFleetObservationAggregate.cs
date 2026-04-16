namespace PitmastersGrill.Models
{
    public class PilotFleetObservationAggregate
    {
        public string CharacterId { get; set; } = "";
        public int AttackerSampleCount { get; set; }
        public int AttackerCountSum { get; set; }

        public double? GetAverageFleetSize()
        {
            if (AttackerSampleCount <= 0)
            {
                return null;
            }

            return (double)AttackerCountSum / AttackerSampleCount;
        }
    }
}