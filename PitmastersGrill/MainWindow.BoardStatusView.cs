using PitmastersGrill.Services;
using System;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private readonly BoardStatusPresenter _boardStatusPresenter = new(TimeProvider.System);
    }
}
