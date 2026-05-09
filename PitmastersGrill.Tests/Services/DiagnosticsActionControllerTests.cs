using PitmastersGrill.Services;
using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class DiagnosticsActionControllerTests
    {
        [Fact]
        public void SetStatus_TrimsMessage()
        {
            RunOnStaThread(() =>
            {
                var statusText = new TextBlock();
                var controller = new DiagnosticsActionController(new Window(), statusText, new BrowserLauncher());

                controller.SetStatus("  Cache refreshed.  ");

                Assert.Equal("Cache refreshed.", statusText.Text);
            });
        }

        [Fact]
        public void SetStatus_UsesDefaultMessageForBlankInput()
        {
            RunOnStaThread(() =>
            {
                var statusText = new TextBlock();
                var controller = new DiagnosticsActionController(new Window(), statusText, new BrowserLauncher());

                controller.SetStatus("   ");

                Assert.Equal("Diagnostics ready.", statusText.Text);
            });
        }

        private static void RunOnStaThread(Action action)
        {
            Exception? capturedException = null;

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (capturedException != null)
            {
                ExceptionDispatchInfo.Capture(capturedException).Throw();
            }
        }
    }
}
