using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace PitmastersGrill.Services
{
    public interface IWindowWorkAreaProvider
    {
        IReadOnlyList<Rect> GetWorkAreasDip(Visual visual);
    }
}
