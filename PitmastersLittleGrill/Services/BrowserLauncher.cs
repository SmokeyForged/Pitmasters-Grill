using System.Diagnostics;

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

            var startInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };

            Process.Start(startInfo);
        }
    }
}