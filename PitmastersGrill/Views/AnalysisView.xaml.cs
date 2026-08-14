using System.Windows.Controls;
using System.Windows.Input;

namespace PitmastersGrill.Views
{
    public partial class AnalysisView : UserControl
    {
        public AnalysisView()
        {
            InitializeComponent();
        }

        public event MouseButtonEventHandler? AllianceListDoubleClick;
        public event MouseButtonEventHandler? CorpListDoubleClick;

        public TextBlock EmptyStateTextBlock => AnalysisEmptyStateText;
        public StackPanel DetailsPanel => AnalysisDetailsPanel;
        public TextBlock VisibleCountsTextBlock => AnalysisVisibleCountsText;
        public TextBlock SignalsTextBlock => AnalysisSignalsText;
        public TextBlock UniqueCountsTextBlock => AnalysisUniqueCountsText;
        public TextBlock AllianceTopTextBlock => AnalysisAllianceTopText;
        public TextBlock CorpTopTextBlock => AnalysisCorpTopText;
        public TextBlock HighlightsTextBlock => AnalysisHighlightsText;
        public ListBox AllianceListBoxControl => AnalysisAllianceListBox;
        public ListBox CorpListBoxControl => AnalysisCorpListBox;
        public TextBlock CurrentCharacterTextBlock => AnalysisCurrentCharacterText;
        public TextBlock CurrentSystemTextBlock => AnalysisCurrentSystemText;
        public TextBlock EvidenceSourceTextBlock => AnalysisEvidenceSourceText;
        public TextBlock ObservedAtTextBlock => AnalysisObservedAtText;
        public TextBlock ContextStatusTextBlock => AnalysisContextStatusText;

        private void AnalysisAllianceListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
            AllianceListDoubleClick?.Invoke(sender, e);

        private void AnalysisCorpListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
            CorpListDoubleClick?.Invoke(sender, e);
    }
}
