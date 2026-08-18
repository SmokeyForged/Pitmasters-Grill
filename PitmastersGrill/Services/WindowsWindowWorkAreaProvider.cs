using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using FormsScreen = System.Windows.Forms.Screen;

namespace PitmastersGrill.Services
{
    public readonly record struct MonitorWorkAreaPixels(double Left, double Top, double Right, double Bottom);

    public sealed class WindowsWindowWorkAreaProvider : IWindowWorkAreaProvider
    {
        private readonly Func<IReadOnlyList<MonitorWorkAreaPixels>> _getMonitorWorkAreas;
        private readonly Func<Visual, Matrix?> _getTransformFromDevice;

        public WindowsWindowWorkAreaProvider()
            : this(
                () => FormsScreen.AllScreens
                    .Select(screen => new MonitorWorkAreaPixels(
                        screen.WorkingArea.Left,
                        screen.WorkingArea.Top,
                        screen.WorkingArea.Right,
                        screen.WorkingArea.Bottom))
                    .ToList(),
                visual => PresentationSource.FromVisual(visual)?.CompositionTarget?.TransformFromDevice)
        {
        }

        public WindowsWindowWorkAreaProvider(
            Func<IReadOnlyList<MonitorWorkAreaPixels>> getMonitorWorkAreas,
            Func<Visual, Matrix?> getTransformFromDevice)
        {
            _getMonitorWorkAreas = getMonitorWorkAreas ?? throw new ArgumentNullException(nameof(getMonitorWorkAreas));
            _getTransformFromDevice = getTransformFromDevice ?? throw new ArgumentNullException(nameof(getTransformFromDevice));
        }

        public IReadOnlyList<Rect> GetWorkAreasDip(Visual visual)
        {
            ArgumentNullException.ThrowIfNull(visual);

            var transform = _getTransformFromDevice(visual) ?? Matrix.Identity;
            return _getMonitorWorkAreas()
                .Select(workArea => ToDipRect(workArea, transform))
                .ToList();
        }

        public static Rect ToDipRect(MonitorWorkAreaPixels workArea, Matrix transformFromDevice)
        {
            var topLeft = transformFromDevice.Transform(new Point(workArea.Left, workArea.Top));
            var bottomRight = transformFromDevice.Transform(new Point(workArea.Right, workArea.Bottom));
            return new Rect(topLeft, bottomRight);
        }
    }
}
