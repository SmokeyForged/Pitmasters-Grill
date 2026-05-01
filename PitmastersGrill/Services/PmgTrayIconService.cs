using PitmastersGrill.Persistence;
using System;
using System.Runtime.Versioning;
using System.Windows.Threading;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsMouseButtons = System.Windows.Forms.MouseButtons;
using FormsMouseEventArgs = System.Windows.Forms.MouseEventArgs;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;
using WpfApplication = System.Windows.Application;
using WpfWindow = System.Windows.Window;
using WpfWindowState = System.Windows.WindowState;

namespace PitmastersGrill.Services
{
    [SupportedOSPlatform("windows6.1")]
    internal sealed class PmgTrayIconService : IDisposable
    {
        private readonly WpfApplication _application;
        private readonly WpfWindow _mainWindow;
        private readonly FormsNotifyIcon _notifyIcon;
        private readonly FormsContextMenuStrip _contextMenu;
        private bool _disposed;

        public PmgTrayIconService(WpfApplication application, WpfWindow mainWindow)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

            _contextMenu = new FormsContextMenuStrip();
            _contextMenu.Items.Add(CreateMenuItem("Show Pitmasters Grill", ShowMainWindow));
            _contextMenu.Items.Add(CreateMenuItem("Exit", ExitApplication));

            _notifyIcon = new FormsNotifyIcon
            {
                Text = "Pitmasters Grill",
                Icon = TryLoadTrayIcon() ?? DrawingSystemIcons.Application,
                ContextMenuStrip = _contextMenu,
                Visible = true
            };

            _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
            _notifyIcon.MouseUp += NotifyIcon_MouseUp;

            AppLogger.UiInfo("PMG notification-area icon initialized.");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                _notifyIcon.DoubleClick -= NotifyIcon_DoubleClick;
                _notifyIcon.MouseUp -= NotifyIcon_MouseUp;
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _contextMenu.Dispose();
                AppLogger.UiInfo("PMG notification-area icon disposed.");
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"Failed to dispose notification-area icon cleanly. error={ex.Message}");
            }
        }

        private static FormsToolStripMenuItem CreateMenuItem(string text, Action action)
        {
            var item = new FormsToolStripMenuItem(text);
            item.Click += (_, __) => action();
            return item;
        }

        private static DrawingIcon? TryLoadTrayIcon()
        {
            try
            {
                var resourceInfo = WpfApplication.GetResourceStream(
                    new Uri("pack://application:,,,/Assets/AppIcon.ico", UriKind.Absolute));

                if (resourceInfo?.Stream == null)
                {
                    AppLogger.UiWarn("PMG tray icon resource was not found. Falling back to default application icon.");
                    return null;
                }

                using var stream = resourceInfo.Stream;
                using var icon = new DrawingIcon(stream);
                return (DrawingIcon)icon.Clone();
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"Failed to load PMG tray icon resource. Falling back to default application icon. error={ex.Message}");
                return null;
            }
        }

        private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        private void NotifyIcon_MouseUp(object? sender, FormsMouseEventArgs e)
        {
            if (e.Button == FormsMouseButtons.Left)
            {
                ShowMainWindow();
            }
        }

        private void ShowMainWindow()
        {
            RunOnDispatcher(() =>
            {
                if (_mainWindow.WindowState == WpfWindowState.Minimized)
                {
                    _mainWindow.WindowState = WpfWindowState.Normal;
                }

                if (!_mainWindow.IsVisible)
                {
                    _mainWindow.Show();
                }

                _mainWindow.Activate();
                _mainWindow.Topmost = true;
                _mainWindow.Topmost = false;
                _mainWindow.Focus();
            });
        }

        private void ExitApplication()
        {
            RunOnDispatcher(() =>
            {
                try
                {
                    _mainWindow.Close();
                }
                catch
                {
                    _application.Shutdown();
                }
            });
        }

        private void RunOnDispatcher(Action action)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                var dispatcher = _mainWindow.Dispatcher ?? _application.Dispatcher;
                if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    return;
                }

                if (dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
                }
            }
            catch (Exception ex)
            {
                AppLogger.UiWarn($"Notification-area icon action failed. error={ex.Message}");
            }
        }
    }
}
