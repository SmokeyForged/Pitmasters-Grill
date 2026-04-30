namespace PitmastersGrill.Models
{
    public class BoardColumnLayoutSetting
    {
        public string ColumnKey { get; set; } = string.Empty;

        public int DisplayIndex { get; set; }

        public double WidthValue { get; set; }

        public string WidthUnitType { get; set; } = "Pixel";
    }
}
