using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace PitmastersGrill.Services
{
    public enum CurrentBoardSessionChangeKind
    {
        RowsChanged,
        BoardRelevantRowStateChanged
    }

    public sealed class CurrentBoardSessionChangedEventArgs : EventArgs
    {
        public CurrentBoardSessionChangedEventArgs(
            CurrentBoardSessionChangeKind kind,
            PilotBoardRow? row = null,
            string? propertyName = null)
        {
            Kind = kind;
            Row = row;
            PropertyName = propertyName ?? string.Empty;
        }

        public CurrentBoardSessionChangeKind Kind { get; }
        public PilotBoardRow? Row { get; }
        public string PropertyName { get; }
    }

    public sealed class CurrentBoardSession : IDisposable
    {
        private static readonly HashSet<string> BoardRelevantPropertyNames = new(StringComparer.Ordinal)
        {
            nameof(PilotBoardRow.IsWatched),
            nameof(PilotBoardRow.BaitOverride),
            nameof(PilotBoardRow.HasDerivedBaitEvidence),
            nameof(PilotBoardRow.BoardSignalKind),
            nameof(PilotBoardRow.CorpName),
            nameof(PilotBoardRow.AllianceName)
        };

        private readonly ObservableCollection<PilotBoardRow> _rows = new();
        private readonly ReadOnlyObservableCollection<PilotBoardRow> _readOnlyRows;
        private bool _disposed;
        private int _processingGeneration;

        public CurrentBoardSession()
        {
            _readOnlyRows = new ReadOnlyObservableCollection<PilotBoardRow>(_rows);
        }

        public event EventHandler<CurrentBoardSessionChangedEventArgs>? Changed;

        public ReadOnlyObservableCollection<PilotBoardRow> Rows => _readOnlyRows;

        public int Count => _rows.Count;

        public int CurrentGeneration => _processingGeneration;

        public int BeginProcessingGeneration()
        {
            ThrowIfDisposed();
            return ++_processingGeneration;
        }

        public bool IsCurrentGeneration(int generation)
        {
            ThrowIfDisposed();
            return generation == _processingGeneration;
        }

        public IReadOnlyList<PilotBoardRow> Snapshot()
        {
            ThrowIfDisposed();
            return _rows.ToList();
        }

        public void ReplaceRows(IEnumerable<PilotBoardRow> rows)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(rows);

            UnsubscribeFromAllRows();
            _rows.Clear();

            foreach (var row in rows)
            {
                if (row == null)
                {
                    continue;
                }

                SubscribeToRow(row);
                _rows.Add(row);
            }

            RaiseChanged(CurrentBoardSessionChangeKind.RowsChanged);
        }

        public bool RemoveRow(PilotBoardRow row)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(row);

            if (!_rows.Remove(row))
            {
                return false;
            }

            UnsubscribeFromRow(row);
            RaiseChanged(CurrentBoardSessionChangeKind.RowsChanged, row);
            return true;
        }

        public int RemoveRows(IEnumerable<PilotBoardRow> rows)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(rows);

            var removedCount = 0;
            foreach (var row in rows.Where(row => row != null).Distinct().ToList())
            {
                if (_rows.Remove(row))
                {
                    UnsubscribeFromRow(row);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                RaiseChanged(CurrentBoardSessionChangeKind.RowsChanged);
            }

            return removedCount;
        }

        public void ReorderRows(IEnumerable<PilotBoardRow> orderedRows)
        {
            ThrowIfDisposed();
            ArgumentNullException.ThrowIfNull(orderedRows);

            var reordered = orderedRows.ToList();
            if (reordered.Count != _rows.Count || reordered.Any(row => !_rows.Contains(row)))
            {
                throw new InvalidOperationException("Board ordering must contain exactly the active session rows.");
            }

            var changed = false;
            for (var index = 0; index < reordered.Count; index++)
            {
                if (!ReferenceEquals(_rows[index], reordered[index]))
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
            {
                return;
            }

            _rows.Clear();
            foreach (var row in reordered)
            {
                _rows.Add(row);
            }

            RaiseChanged(CurrentBoardSessionChangeKind.RowsChanged);
        }

        public int ClearAndInvalidate()
        {
            ThrowIfDisposed();
            var generation = ++_processingGeneration;
            UnsubscribeFromAllRows();
            _rows.Clear();
            RaiseChanged(CurrentBoardSessionChangeKind.RowsChanged);
            return generation;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            UnsubscribeFromAllRows();
            _rows.Clear();
            _disposed = true;
        }

        private void SubscribeToRow(PilotBoardRow row)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
            row.PropertyChanged += OnRowPropertyChanged;
        }

        private void UnsubscribeFromRow(PilotBoardRow row)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }

        private void UnsubscribeFromAllRows()
        {
            foreach (var row in _rows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
            }
        }

        private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not PilotBoardRow row ||
                string.IsNullOrWhiteSpace(e.PropertyName) ||
                !BoardRelevantPropertyNames.Contains(e.PropertyName))
            {
                return;
            }

            RaiseChanged(
                CurrentBoardSessionChangeKind.BoardRelevantRowStateChanged,
                row,
                e.PropertyName);
        }

        private void RaiseChanged(
            CurrentBoardSessionChangeKind kind,
            PilotBoardRow? row = null,
            string? propertyName = null)
        {
            Changed?.Invoke(this, new CurrentBoardSessionChangedEventArgs(kind, row, propertyName));
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }
    }
}
