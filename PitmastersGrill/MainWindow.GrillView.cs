using System.Windows.Controls;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private DataGrid PilotBoard => GrillViewControl.PilotBoardControl;
        private Grid BoardOverlayHost => GrillViewControl.BoardOverlayHostGrid;
        private Border BoardModeHintOverlay => GrillViewControl.BoardModeHintOverlayControl;
        private DataGridColumn SigColumn => GrillViewControl.SigColumnControl;
        private DataGridColumn CharacterColumn => GrillViewControl.CharacterColumnControl;
        private DataGridColumn AllianceColumn => GrillViewControl.AllianceColumnControl;
        private DataGridColumn CorpColumn => GrillViewControl.CorpColumnControl;
        private DataGridColumn KillsColumn => GrillViewControl.KillsColumnControl;
        private DataGridColumn LossesColumn => GrillViewControl.LossesColumnControl;
        private DataGridColumn AvgFleetSizeColumn => GrillViewControl.AvgFleetSizeColumnControl;
        private DataGridColumn LastShipSeenColumn => GrillViewControl.LastShipSeenColumnControl;
        private DataGridColumn LastSeenColumn => GrillViewControl.LastSeenColumnControl;
        private DataGridColumn CynoHullSeenColumn => GrillViewControl.CynoHullSeenColumnControl;
    }
}
