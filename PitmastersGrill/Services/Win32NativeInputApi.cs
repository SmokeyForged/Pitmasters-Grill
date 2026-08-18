using System;
using System.Runtime.InteropServices;

namespace PitmastersGrill.Services
{
    public sealed class Win32NativeInputApi : INativeInputApi
    {
        public bool AddClipboardFormatListener(IntPtr hwnd) => NativeAddClipboardFormatListener(hwnd);

        public bool RemoveClipboardFormatListener(IntPtr hwnd) => NativeRemoveClipboardFormatListener(hwnd);

        public bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey) =>
            NativeRegisterHotKey(hwnd, id, modifiers, virtualKey);

        public bool UnregisterHotKey(IntPtr hwnd, int id) => NativeUnregisterHotKey(hwnd, id);

        public int GetLastError() => Marshal.GetLastWin32Error();

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "AddClipboardFormatListener")]
        private static extern bool NativeAddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "RemoveClipboardFormatListener")]
        private static extern bool NativeRemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "RegisterHotKey")]
        private static extern bool NativeRegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "UnregisterHotKey")]
        private static extern bool NativeUnregisterHotKey(IntPtr hwnd, int id);
    }
}
