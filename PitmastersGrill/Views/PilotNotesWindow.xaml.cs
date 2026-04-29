using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System;
using System.Windows;
using System.Windows.Input;

namespace PitmastersGrill.Views
{
    public partial class PilotNotesWindow : Window
    {
        private readonly PilotBoardRow _row;
        private readonly NotesRepository _notesRepository;
        private bool _saved;

        public PilotNotesWindow(PilotBoardRow row, NotesRepository notesRepository)
        {
            _row = row ?? throw new ArgumentNullException(nameof(row));
            _notesRepository = notesRepository ?? throw new ArgumentNullException(nameof(notesRepository));

            InitializeComponent();

            Title = $"PMG Notes - {_row.CharacterName}";
            PilotNameText.Text = _row.CharacterName;
            NotesTextBox.Text = _notesRepository.GetNotes(_row.CharacterName);
            NotesTextBox.Focus();
            NotesTextBox.CaretIndex = NotesTextBox.Text?.Length ?? 0;
        }

        public bool Saved => _saved;

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Save();
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Save();
                Close();
                e.Handled = true;
            }
        }

        private void Save()
        {
            _notesRepository.SaveNotes(_row.CharacterName, NotesTextBox.Text ?? string.Empty);
            _row.HasNotes = _notesRepository.HasNotes(_row.CharacterName);
            _saved = true;
        }
    }
}
