using System;

namespace PitmastersGrill.Services
{
    public interface INativeInputApi
    {
        bool AddClipboardFormatListener(IntPtr hwnd);
        bool RemoveClipboardFormatListener(IntPtr hwnd);
        bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);
        bool UnregisterHotKey(IntPtr hwnd, int id);
        int GetLastError();
    }
}
