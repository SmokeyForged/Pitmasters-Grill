namespace PitmastersLittleGrill.Models
{
    public class StartupUpdateState
    {
        public string StatusText { get; set; } = "";
        public string DetailText { get; set; } = "";
        public bool IsIndeterminate { get; set; }
        public double ProgressValue { get; set; }
        public bool IsExceptionMessage { get; set; }
    }
}