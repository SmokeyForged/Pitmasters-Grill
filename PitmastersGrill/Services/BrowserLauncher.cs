using PitmastersGrill.Persistence;
using System;
using System.Diagnostics;
using System.IO;

namespace PitmastersGrill.Services
{
    public sealed record BrowserLaunchResult(
        bool Attempted,
        bool Succeeded,
        string Url,
        Exception? Exception = null);

    public class BrowserLauncher
    {
        public void OpenUrl(string url)
        {
            var result = TryOpenUrl(url);
            if (!result.Attempted)
            {
                return;
            }

            if (result.Succeeded)
            {
                AppLogger.UiInfo($"Opened external URL. url={result.Url}");
                return;
            }

            if (result.Exception != null)
            {
                AppLogger.UiError($"Failed to open external URL. url={result.Url}", result.Exception);
                return;
            }

            AppLogger.UiWarn($"Failed to open external URL. url={result.Url}");
        }

        public BrowserLaunchResult TryOpenUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return new BrowserLaunchResult(false, false, string.Empty);
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                return new BrowserLaunchResult(true, true, url);
            }
            catch (Exception ex)
            {
                return new BrowserLaunchResult(true, false, url, ex);
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
