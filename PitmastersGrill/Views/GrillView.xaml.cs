using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PitmastersGrill.Views
{
    public partial class GrillView : UserControl
    {
        public GrillView()
        {
            InitializeComponent();
        }

        public event DataGridSortingEventHandler? Sorting;
        public event EventHandler<DataGridColumnEventArgs>? ColumnReordered;
        public event SelectionChangedEventHandler? BoardSelectionChanged;
        public event MouseButtonEventHandler? BoardMouseDoubleClick;
        public event MouseButtonEventHandler? BoardPreviewMouseRightButtonUp;
        public event MouseButtonEventHandler? BoardPreviewMouseLeftButtonDown;
        public event MouseButtonEventHandler? BoardPreviewMouseLeftButtonUp;
        public event MouseEventHandler? BoardPreviewMouseMove;
        public event SizeChangedEventHandler? BoardSizeChanged;
        public event RoutedEventHandler? PilotNoteClick;

        public Grid BoardOverlayHostGrid => BoardOverlayHost;
        public DataGrid PilotBoardControl => PilotBoard;
        public Border BoardModeHintOverlayControl => BoardModeHintOverlay;
        public DataGridColumn SigColumnControl => SigColumn;
        public DataGridColumn CharacterColumnControl => CharacterColumn;
        public DataGridColumn AllianceColumnControl => AllianceColumn;
        public DataGridColumn CorpColumnControl => CorpColumn;
        public DataGridColumn KillsColumnControl => KillsColumn;
        public DataGridColumn LossesColumnControl => LossesColumn;
        public DataGridColumn AvgFleetSizeColumnControl => AvgFleetSizeColumn;
        public DataGridColumn LastShipSeenColumnControl => LastShipSeenColumn;
        public DataGridColumn LastSeenColumnControl => LastSeenColumn;
        public DataGridColumn CynoHullSeenColumnControl => CynoHullSeenColumn;

        private void PilotBoard_Sorting(object sender, DataGridSortingEventArgs e) => Sorting?.Invoke(sender, e);
        private void PilotBoard_ColumnReordered(object sender, DataGridColumnEventArgs e) => ColumnReordered?.Invoke(sender, e);
        private void PilotBoard_SelectionChanged(object sender, SelectionChangedEventArgs e) => BoardSelectionChanged?.Invoke(sender, e);
        private void PilotBoard_MouseDoubleClick(object sender, MouseButtonEventArgs e) => BoardMouseDoubleClick?.Invoke(sender, e);
        private void PilotBoard_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) => BoardPreviewMouseRightButtonUp?.Invoke(sender, e);
        private void PilotBoard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => BoardPreviewMouseLeftButtonDown?.Invoke(sender, e);
        private void PilotBoard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => BoardPreviewMouseLeftButtonUp?.Invoke(sender, e);
        private void PilotBoard_PreviewMouseMove(object sender, MouseEventArgs e) => BoardPreviewMouseMove?.Invoke(sender, e);
        private void PilotBoard_SizeChanged(object sender, SizeChangedEventArgs e) => BoardSizeChanged?.Invoke(sender, e);
        private void PilotNoteButton_Click(object sender, RoutedEventArgs e) => PilotNoteClick?.Invoke(sender, e);
    }
}
