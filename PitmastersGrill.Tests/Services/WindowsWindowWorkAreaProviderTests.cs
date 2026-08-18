using PitmastersGrill.Services;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class WindowsWindowWorkAreaProviderTests
    {
        [Fact]
        public void GetWorkAreasDip_ConvertsDevicePixelsUsingInjectedTransform()
        {
            var provider = new WindowsWindowWorkAreaProvider(
                () => new[] { new MonitorWorkAreaPixels(0, 0, 1920, 1080) },
                _ => new Matrix(0.5, 0, 0, 0.5, 0, 0));

            var result = provider.GetWorkAreasDip(new DrawingVisual());

            Assert.Equal(new Rect(0, 0, 960, 540), Assert.Single(result));
        }

        [Fact]
        public void GetWorkAreasDip_UsesIdentityTransformWhenCompositionTransformIsUnavailable()
        {
            var provider = new WindowsWindowWorkAreaProvider(
                () => new[] { new MonitorWorkAreaPixels(-1280, 0, 0, 1024) },
                _ => null);

            var result = provider.GetWorkAreasDip(new DrawingVisual());

            Assert.Equal(new Rect(-1280, 0, 1280, 1024), Assert.Single(result));
        }

        [Fact]
        public void GetWorkAreasDip_PreservesMonitorEnumerationOrder()
        {
            IReadOnlyList<MonitorWorkAreaPixels> monitors = new[]
            {
                new MonitorWorkAreaPixels(0, 0, 1920, 1080),
                new MonitorWorkAreaPixels(-1280, 0, 0, 1024)
            };
            var provider = new WindowsWindowWorkAreaProvider(
                () => monitors,
                _ => Matrix.Identity);

            var result = provider.GetWorkAreasDip(new DrawingVisual());

            Assert.Equal(2, result.Count);
            Assert.Equal(new Rect(0, 0, 1920, 1080), result[0]);
            Assert.Equal(new Rect(-1280, 0, 1280, 1024), result[1]);
        }

        [Fact]
        public void GetWorkAreasDip_DoesNotOwnWindowBoundsValidationPolicy()
        {
            var provider = new WindowsWindowWorkAreaProvider(
                () => new[] { new MonitorWorkAreaPixels(10, 20, 10, 20) },
                _ => Matrix.Identity);

            var result = provider.GetWorkAreasDip(new DrawingVisual());

            Assert.Single(result);
            Assert.False(new WindowLayoutController().IsUsableWindowBounds(result[0]));
        }
    }
}
