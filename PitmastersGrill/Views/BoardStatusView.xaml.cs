using System.Windows.Controls;

namespace PitmastersGrill.Views
{
    public partial class BoardStatusView : UserControl
    {
        public BoardStatusView()
        {
            InitializeComponent();
        }

        public Border FooterBorder => BoardStatusFooter;
        public TextBlock PopulationStatusTextBlock => BoardPopulationStatusText;
        public TextBlock LastRefreshedTextBlock => LastRefreshedText;
        public TextBlock SummaryTextBlock => BoardSummaryText;
    }
}
