using System;

namespace PitmastersGrill.Services
{
    public enum PilotDetailWindowOpenAction
    {
        None = 0,
        CreateNew,
        ActivateExisting,
        ReplaceExisting
    }

    public sealed class PilotDetailWindowLifecycleController
    {
        private string _activeCharacterName = string.Empty;

        public bool HasActiveWindow => !string.IsNullOrWhiteSpace(_activeCharacterName);
        public string ActiveCharacterName => _activeCharacterName;

        public PilotDetailWindowOpenAction DecideOpenAction(string targetCharacterName)
        {
            if (string.IsNullOrWhiteSpace(targetCharacterName))
            {
                return PilotDetailWindowOpenAction.None;
            }

            if (!HasActiveWindow)
            {
                return PilotDetailWindowOpenAction.CreateNew;
            }

            return string.Equals(_activeCharacterName, targetCharacterName, StringComparison.OrdinalIgnoreCase)
                ? PilotDetailWindowOpenAction.ActivateExisting
                : PilotDetailWindowOpenAction.ReplaceExisting;
        }

        public void MarkWindowOpened(string characterName)
        {
            _activeCharacterName = characterName ?? string.Empty;
        }

        public bool ShouldRefreshActiveWindow(string rowCharacterName)
        {
            return HasActiveWindow &&
                   !string.IsNullOrWhiteSpace(rowCharacterName) &&
                   string.Equals(_activeCharacterName, rowCharacterName, StringComparison.OrdinalIgnoreCase);
        }

        public void ClearActiveWindow()
        {
            _activeCharacterName = string.Empty;
        }
    }
}
