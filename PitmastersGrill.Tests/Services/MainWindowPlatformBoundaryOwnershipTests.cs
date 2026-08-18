using System;
using System.IO;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class MainWindowPlatformBoundaryOwnershipTests
    {
        [Fact]
        public void MainWindow_DoesNotOwnRawNativeInputApis()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");

            Assert.DoesNotContain("DllImport", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Marshal.GetLastWin32Error", source, StringComparison.Ordinal);
            Assert.DoesNotContain("static extern bool RegisterHotKey", source, StringComparison.Ordinal);
            Assert.DoesNotContain("static extern bool AddClipboardFormatListener", source, StringComparison.Ordinal);
            Assert.Contains("_nativeInputApi", source, StringComparison.Ordinal);
        }

        [Fact]
        public void MainWindow_DoesNotClaimClipboardListenerSuccessUnconditionally()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");

            Assert.DoesNotContain("Clipboard listener attached", source, StringComparison.Ordinal);
            Assert.DoesNotContain("Clipboard listener removed", source, StringComparison.Ordinal);
            Assert.Contains("Native input attach attempt complete", source, StringComparison.Ordinal);
            Assert.Contains("Native input detach attempt complete", source, StringComparison.Ordinal);
        }

        [Fact]
        public void MainWindow_DoesNotOwnMonitorEnumerationOrDpiConversion()
        {
            var source = ReadRepoFile("PitmastersGrill", "MainWindow.xaml.cs");
            var constructorSource = ReadRepoFile("PitmastersGrill", "MainWindow.ComposedConstructor.cs");

            Assert.DoesNotContain("FormsScreen.AllScreens", source, StringComparison.Ordinal);
            Assert.DoesNotContain("GetMonitorWorkAreasDip", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DevicePixelsToDip", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PresentationSource.FromVisual(this)", source, StringComparison.Ordinal);
            Assert.Contains("_windowWorkAreaProvider.GetWorkAreasDip(this)", constructorSource, StringComparison.Ordinal);
        }

        [Fact]
        public void RawPlatformMechanics_AreOwnedByFocusedAdapters()
        {
            var nativeSource = ReadRepoFile("PitmastersGrill", "Services", "Win32NativeInputApi.cs");
            var workAreaSource = ReadRepoFile("PitmastersGrill", "Services", "WindowsWindowWorkAreaProvider.cs");

            Assert.Contains("DllImport", nativeSource, StringComparison.Ordinal);
            Assert.Contains("FormsScreen.AllScreens", workAreaSource, StringComparison.Ordinal);
            Assert.Contains("TransformFromDevice", workAreaSource, StringComparison.Ordinal);
        }

        private static string ReadRepoFile(params string[] relativeSegments)
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var candidateSegments = new string[relativeSegments.Length + 1];
                candidateSegments[0] = current.FullName;
                Array.Copy(relativeSegments, 0, candidateSegments, 1, relativeSegments.Length);
                var candidate = Path.Combine(candidateSegments);
                if (File.Exists(candidate))
                {
                    return File.ReadAllText(candidate);
                }

                current = current.Parent;
            }

            throw new FileNotFoundException($"Could not locate repository file: {string.Join("/", relativeSegments)}");
        }
    }
}
