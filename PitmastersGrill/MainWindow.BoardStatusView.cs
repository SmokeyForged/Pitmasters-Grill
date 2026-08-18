using PitmastersGrill.Services;
using System;
using System.Windows.Controls;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private readonly BoardStatusPresenter _boardStatusPresenter = new(TimeProvider.System);

        private Border BoardStatusFooter => BoardStatusViewControl.FooterBorder;
        private TextBlock BoardPopulationStatusText => BoardStatusViewControl.PopulationStatusTextBlock;
        private TextBlock LastRefreshedText => BoardStatusViewControl.LastRefreshedTextBlock;
        private TextBlock BoardSummaryText => BoardStatusViewControl.SummaryTextBlock;
    }
}
