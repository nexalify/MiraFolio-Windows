using System.Runtime.InteropServices;
using System.Text;

namespace MiraFolio.App.DesktopOverlay;

internal static class DesktopOverlayNativeMethods
{
    internal const int GwlExStyle = -20;
    internal const long WsExToolWindow = 0x00000080L;
    internal const long WsExTransparent = 0x00000020L;
    internal const long WsExNoActivate = 0x08000000L;
    internal const uint EventSystemForeground = 0x0003;
    internal const uint WineventOutOfContext = 0x0000;
    internal const int WmMouseActivate = 0x0021;
    internal const int WmDpiChanged = 0x02E0;
    internal const int MaNoActivate = 3;
    internal const int SwShowNoActivate = 4;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpNoOwnerZOrder = 0x0200;
    internal const uint SwpShowWindow = 0x0040;

    internal static readonly nint HwndTopmost = new(-1);

    internal delegate void WinEventDelegate(
        nint winEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint eventThread,
        uint eventTime);

    internal delegate bool EnumWindowsDelegate(nint hwnd, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect(int left, int top, int right, int bottom)
    {
        internal int Left = left;
        internal int Top = top;
        internal int Right = right;
        internal int Bottom = bottom;

        internal bool Contains(Point point) =>
            point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;

        internal bool Intersects(Rect other) =>
            Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
    }

    [DllImport("user32.dll")]
    internal static extern nint GetShellWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsDelegate callback, nint lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ScreenToClient(nint hwnd, ref Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(nint hwnd, out Rect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    internal static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint eventHookModule,
        WinEventDelegate eventHookProc,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint winEventHook);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        nint hwnd,
        nint hwndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint hwnd, int command);

    [DllImport("user32.dll")]
    internal static extern uint GetDpiForWindow(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr64(nint hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(nint hwnd, int index, int newLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr64(nint hwnd, int index, nint newLong);

    internal static nint GetWindowLongPtr(nint hwnd, int index) =>
        nint.Size == 8 ? GetWindowLongPtr64(hwnd, index) : new nint(GetWindowLong32(hwnd, index));

    internal static void SetWindowLongPtr(nint hwnd, int index, nint newLong)
    {
        if (nint.Size == 8)
            SetWindowLongPtr64(hwnd, index, newLong);
        else
            SetWindowLong32(hwnd, index, newLong.ToInt32());
    }

    internal static bool IsPointWithinWindowClient(nint hwnd, Point screenPoint) =>
        ScreenToClient(hwnd, ref screenPoint) &&
        GetClientRect(hwnd, out var clientBounds) &&
        clientBounds.Contains(screenPoint);

    internal static bool IsDesktopAreaUncovered(Rect area, IReadOnlySet<nint> ignoredWindows)
    {
        var shellWindow = GetShellWindow();
        var desktopReached = false;
        var obstructed = false;

        EnumWindows((hwnd, _) =>
        {
            if (ignoredWindows.Contains(hwnd) || !IsWindowVisible(hwnd) || IsIconic(hwnd))
                return true;
            if (!GetWindowRect(hwnd, out var windowRect) || !windowRect.Intersects(area))
                return true;

            var extendedStyle = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            if ((extendedStyle & WsExTransparent) != 0)
                return true;

            if (hwnd == shellWindow || IsDesktopWindow(hwnd))
            {
                desktopReached = true;
                return false;
            }

            obstructed = true;
            return false;
        }, nint.Zero);

        return desktopReached && !obstructed;
    }

    private static bool IsDesktopWindow(nint hwnd)
    {
        var className = new StringBuilder(64);
        return GetClassName(hwnd, className, className.Capacity) != 0 &&
            className.ToString() is "Progman" or "WorkerW";
    }
}
