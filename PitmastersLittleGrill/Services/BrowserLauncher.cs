using PitmastersLittleGrill.Persistence;
using System;
using System.Diagnostics;
using System.IO;

namespace PitmastersLittleGrill.Services
{
    public class BrowserLauncher
    {
        public void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                AppLogger.UiInfo($"Opened external URL. url={url}");
            }
            catch (Exception ex)
            {
                AppLogger.UiError($"Failed to open external URL. url={url}", ex);
            }
        }

        public void OpenPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    AppLogger.UiWarn($"Requested path open, but path does not exist. path={path}");
                    return;
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                AppLogger.UiInfo($"Opened local path. path={path}");
            }
            catch (Exception ex)
            {
                AppLogger.UiError($"Failed to open local path. path={path}", ex);
            }
        }
    }
}