namespace PitmastersGrill.Services
{
    public sealed class CompactBoardDragController
    {
        public bool IsPending { get; private set; }

        public bool TryBegin(bool boardModeEnabled, int clickCount, bool blockedByInteractiveElement)
        {
            // Preserve MainWindow's existing routed-event behavior: duplicate delivery while a
            // drag is already pending is ignored before any later eligibility checks run.
            if (IsPending || !boardModeEnabled)
            {
                return false;
            }

            if (clickCount > 1)
            {
                Cancel();
                return false;
            }

            if (blockedByInteractiveElement)
            {
                return false;
            }

            IsPending = true;
            return true;
        }

        public void Cancel()
        {
            IsPending = false;
        }

        public bool CancelIfLeftButtonReleased(bool leftButtonPressed)
        {
            if (!IsPending || leftButtonPressed)
            {
                return false;
            }

            Cancel();
            return true;
        }

        public bool CompleteHold(bool boardModeEnabled, bool leftButtonPressed)
        {
            if (!IsPending || !boardModeEnabled || !leftButtonPressed)
            {
                Cancel();
                return false;
            }

            IsPending = false;
            return true;
        }
    }
}
