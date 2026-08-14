using System.Windows.Controls;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private TextBlock AnalysisEmptyStateText => AnalysisViewControl.EmptyStateTextBlock;
        private StackPanel AnalysisDetailsPanel => AnalysisViewControl.DetailsPanel;
        private TextBlock AnalysisVisibleCountsText => AnalysisViewControl.VisibleCountsTextBlock;
        private TextBlock AnalysisSignalsText => AnalysisViewControl.SignalsTextBlock;
        private TextBlock AnalysisUniqueCountsText => AnalysisViewControl.UniqueCountsTextBlock;
        private TextBlock AnalysisAllianceTopText => AnalysisViewControl.AllianceTopTextBlock;
        private TextBlock AnalysisCorpTopText => AnalysisViewControl.CorpTopTextBlock;
        private TextBlock AnalysisHighlightsText => AnalysisViewControl.HighlightsTextBlock;
        private ListBox AnalysisAllianceListBox => AnalysisViewControl.AllianceListBoxControl;
        private ListBox AnalysisCorpListBox => AnalysisViewControl.CorpListBoxControl;
        private TextBlock AnalysisCurrentCharacterText => AnalysisViewControl.CurrentCharacterTextBlock;
        private TextBlock AnalysisCurrentSystemText => AnalysisViewControl.CurrentSystemTextBlock;
        private TextBlock AnalysisEvidenceSourceText => AnalysisViewControl.EvidenceSourceTextBlock;
        private TextBlock AnalysisObservedAtText => AnalysisViewControl.ObservedAtTextBlock;
        private TextBlock AnalysisContextStatusText => AnalysisViewControl.ContextStatusTextBlock;
    }
}
