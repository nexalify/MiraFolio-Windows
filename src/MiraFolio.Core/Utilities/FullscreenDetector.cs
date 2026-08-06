using System.Runtime.InteropServices;
using MiraFolio.Core.Models;

namespace MiraFolio.Core.Utilities;

public static class FullscreenDetector
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    public static bool IsFullscreenOnMonitor(MonitorInfo monitor)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
            return false;

        if (!GetWindowRect(foreground, out var rect))
            return false;

        return rect.left <= monitor.Left
            && rect.top <= monitor.Top
            && rect.right >= monitor.Left + monitor.Width
            && rect.bottom >= monitor.Top + monitor.Height;
    }
}
