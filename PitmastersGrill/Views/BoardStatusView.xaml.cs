using System.Windows.Controls;
using System.Windows.Media;

namespace PitmastersGrill.Views
{
    public partial class BoardStatusView : UserControl
    {
        public BoardStatusView()
        {
            InitializeComponent();
        }

        public Border FooterBorder => BoardStatusFooter;

        public void SetPopulationStatusText(string text) =>
            BoardPopulationStatusText.Text = text ?? string.Empty;

        public void SetPopulationStatusForeground(Brush brush) =>
            BoardPopulationStatusText.Foreground = brush;

        public void SetLastRefreshedText(string text) =>
            LastRefreshedText.Text = text ?? string.Empty;

        public void SetSummaryText(string text) =>
            BoardSummaryText.Text = text ?? string.Empty;
    }
}
