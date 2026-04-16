namespace PitmastersGrill.Models
{
    public enum EnrichmentStageState
    {
        NotStarted,
        Success,
        NotFound,
        Throttled,
        TemporaryFailure,
        PermanentFailure,
        Skipped
    }
}