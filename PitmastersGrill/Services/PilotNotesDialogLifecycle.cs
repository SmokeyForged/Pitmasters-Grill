using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using PitmastersGrill.Views;
using System;
using System.Windows;

namespace PitmastersGrill.Services
{
    public readonly record struct PilotNotesDialogResult(bool Saved, bool HasNotes);

    public interface IPilotNotesDialog
    {
        bool Saved { get; }

        void ConfigureOwner(object owner, bool topmost);

        void CopyResourcesFrom(ResourceDictionary resources);

        void ShowDialog();
    }

    public interface IPilotNotesDialogFactory
    {
        IPilotNotesDialog Create(PilotBoardRow row);
    }

    public sealed class PilotNotesDialogLifecycle
    {
        private readonly IPilotNotesDialogFactory _dialogFactory;
        private readonly Func<string, bool> _hasNotes;

        public PilotNotesDialogLifecycle(NotesRepository notesRepository)
            : this(
                new PilotNotesDialogFactory(notesRepository ?? throw new ArgumentNullException(nameof(notesRepository))),
                notesRepository.HasNotes)
        {
        }

        public PilotNotesDialogLifecycle(
            IPilotNotesDialogFactory dialogFactory,
            Func<string, bool> hasNotes)
        {
            _dialogFactory = dialogFactory ?? throw new ArgumentNullException(nameof(dialogFactory));
            _hasNotes = hasNotes ?? throw new ArgumentNullException(nameof(hasNotes));
        }

        public PilotNotesDialogResult Open(
            PilotBoardRow row,
            object owner,
            bool topmost,
            ResourceDictionary resources)
        {
            ArgumentNullException.ThrowIfNull(row);
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(resources);

            var dialog = _dialogFactory.Create(row);
            dialog.ConfigureOwner(owner, topmost);
            dialog.CopyResourcesFrom(resources);
            dialog.ShowDialog();

            var hasNotes = _hasNotes(row.CharacterName);
            row.HasNotes = hasNotes;

            return new PilotNotesDialogResult(dialog.Saved, hasNotes);
        }
    }

    public sealed class PilotNotesDialogFactory : IPilotNotesDialogFactory
    {
        private readonly NotesRepository _notesRepository;

        public PilotNotesDialogFactory(NotesRepository notesRepository)
        {
            _notesRepository = notesRepository ?? throw new ArgumentNullException(nameof(notesRepository));
        }

        public IPilotNotesDialog Create(PilotBoardRow row)
        {
            ArgumentNullException.ThrowIfNull(row);
            return new WpfPilotNotesDialog(new PilotNotesWindow(row, _notesRepository));
        }

        private sealed class WpfPilotNotesDialog : IPilotNotesDialog
        {
            private readonly PilotNotesWindow _window;

            public WpfPilotNotesDialog(PilotNotesWindow window)
            {
                _window = window ?? throw new ArgumentNullException(nameof(window));
            }

            public bool Saved => _window.Saved;

            public void ConfigureOwner(object owner, bool topmost)
            {
                if (owner is not Window ownerWindow)
                {
                    throw new ArgumentException("Pilot Notes owner must be a WPF Window.", nameof(owner));
                }

                _window.Owner = ownerWindow;
                _window.Topmost = topmost;
            }

            public void CopyResourcesFrom(ResourceDictionary resources)
            {
                ArgumentNullException.ThrowIfNull(resources);

                _window.Resources.MergedDictionaries.Clear();
                foreach (var key in resources.Keys)
                {
                    _window.Resources[key] = resources[key];
                }
            }

            public void ShowDialog()
            {
                _window.ShowDialog();
            }
        }
    }
}
