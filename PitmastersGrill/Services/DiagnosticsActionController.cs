using PitmastersGrill.Diagnostics;
using PitmastersGrill.Persistence;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PitmastersGrill.Services
{
    public sealed class DiagnosticsActionController
    {
        private readonly Window _owner;
        private readonly TextBlock _diagnosticsStatusText;
        private readonly BrowserLauncher _browserLauncher;

        public DiagnosticsActionController(
            Window owner,
            TextBlock diagnosticsStatusText,
            BrowserLauncher browserLauncher)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _diagnosticsStatusText = diagnosticsStatusText ?? throw new ArgumentNullException(nameof(diagnosticsStatusText));
            _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
        }

        public void OpenLogs()
        {
            try
            {
                var logsRootPath = AppPaths.GetLogsRootDirectory();

                AppLogger.UiInfo($"Open logs requested.\npath={logsRootPath}");
                SetStatus("Opening logs folder.");

                _browserLauncher.OpenPath(logsRootPath);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Open logs failed.", ex);
                SetStatus("Failed to open logs folder.");

                MessageBox.Show(
                    _owner,
                    $"Failed to open logs folder.\n\n{ex.Message}",
                    "PMG Logs Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void PackageDiagnostics()
        {
            try
            {
                var bundlePath = DiagnosticBundleService.TryCreateBundle("manual-diagnostics-package");

                if (string.IsNullOrWhiteSpace(bundlePath))
                {
                    SetStatus("Diagnostic package failed.");

                    MessageBox.Show(
                        _owner,
                        "PMG could not create a diagnostic package.\nCheck the active logs for details.",
                        "PMG Diagnostics",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    return;
                }

                var bundleFileName = Path.GetFileName(bundlePath);

                SetStatus($"Created diagnostic package: {bundleFileName}");
                AppLogger.UiInfo($"Manual diagnostic package created.\npath={bundlePath}");

                var diagnosticsDirectory = Path.GetDirectoryName(bundlePath);
                if (!string.IsNullOrWhiteSpace(diagnosticsDirectory))
                {
                    _browserLauncher.OpenPath(diagnosticsDirectory);
                }
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Manual diagnostic package failed.", ex);
                SetStatus("Diagnostic package failed.");

                MessageBox.Show(
                    _owner,
                    $"Failed to create diagnostic package.\n\n{ex.Message}",
                    "PMG Diagnostics Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void OpenDiagnosticsFolder()
        {
            try
            {
                var diagnosticsDirectory = DiagnosticBundleService.GetDiagnosticsDirectory();

                AppLogger.UiInfo($"Open diagnostics folder requested.\npath={diagnosticsDirectory}");
                SetStatus("Opening diagnostics folder.");

                _browserLauncher.OpenPath(diagnosticsDirectory);
            }
            catch (Exception ex)
            {
                AppLogger.UiError("Open diagnostics folder failed.", ex);
                SetStatus("Failed to open diagnostics folder.");

                MessageBox.Show(
                    _owner,
                    $"Failed to open diagnostics folder.\n\n{ex.Message}",
                    "PMG Diagnostics Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void SetStatus(string message)
        {
            _diagnosticsStatusText.Text = string.IsNullOrWhiteSpace(message)
                ? "Diagnostics ready."
                : message.Trim();
        }
    }
}
