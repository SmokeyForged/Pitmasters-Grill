using PitmastersGrill.Persistence;
using System;
using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace PitmastersGrill.Services
{
    public sealed class SystemTrayIconService : IDisposable
    {
        private readonly Window _window;
        private readonly Action _exitRequested;
        private readonly Forms.NotifyIcon _notifyIcon;
        private bool _disposed;

        public SystemTrayIconService(Window window, Action exitRequested)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _exitRequested = exitRequested ?? throw new ArgumentNullException(nameof(exitRequested));

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Show / Restore", null, (_, _) => RestoreWindow());
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => _window.Dispatcher.Invoke(_exitRequested));

            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = LoadIcon(),
                Text = "Pitmasters Grill",
                ContextMenuStrip = menu,
                Visible = true
            };

            _notifyIcon.DoubleClick += (_, _) => _window.Dispatcher.Invoke(RestoreWindow);
        }

        private void RestoreWindow()
        {
            if (_disposed)
            {
                return;
            }

            if (!_window.IsVisible)
            {
                _window.Show();
            }

            if (_window.WindowState == WindowState.Minimized)
            {
                _window.WindowState = WindowState.Normal;
            }

            _window.Activate();
        }

        private static Drawing.Icon LoadIcon()
        {
            try
            {
                var streamInfo = Application.GetResourceStream(new Uri("Assets/PMGIcon.ico", UriKind.Relative));
                if (streamInfo?.Stream != null)
                {
                    using var stream = streamInfo.Stream;
                    return new Drawing.Icon(stream);
                }
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"Failed to load PMG tray icon resource. {ex.Message}");
            }

            return Drawing.SystemIcons.Application;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
        }
    }
}
