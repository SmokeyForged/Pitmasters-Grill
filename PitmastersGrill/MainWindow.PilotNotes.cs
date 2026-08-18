using PitmastersGrill.Services;

namespace PitmastersGrill
{
    public partial class MainWindow
    {
        private PilotNotesDialogLifecycle? _pilotNotesDialogLifecycle;

        private PilotNotesDialogLifecycle PilotNotesDialogLifecycle =>
            _pilotNotesDialogLifecycle ??= new PilotNotesDialogLifecycle(_notesRepository);
    }
}
