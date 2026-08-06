using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace MiraFolio.Core.Interop;

[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    [PreserveSig] int SetWallpaper(
        [MarshalAs(UnmanagedType.LPWStr)] string monitorID,
        [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

    [PreserveSig] int GetWallpaper(
        [MarshalAs(UnmanagedType.LPWStr)] string monitorID,
        [MarshalAs(UnmanagedType.LPWStr)] out string wallpaper);

    [PreserveSig] int GetMonitorDevicePathAt(
        uint monitorIndex,
        [MarshalAs(UnmanagedType.LPWStr)] out string monitorID);

    [PreserveSig] int GetMonitorDevicePathCount(out uint count);

    [PreserveSig] int GetMonitorRECT(
        [MarshalAs(UnmanagedType.LPWStr)] string monitorID,
        out RECT displayRect);

    [PreserveSig] int SetBackgroundColor(uint color);
    [PreserveSig] int GetBackgroundColor(out uint color);
    [PreserveSig] int SetPosition(int position);
    [PreserveSig] int GetPosition(out int position);
    [PreserveSig] int SetSlideshow(IntPtr items);
    [PreserveSig] int GetSlideshow(out IntPtr items);
    [PreserveSig] int SetSlideshowOptions(uint options, uint slideshowTick);
    [PreserveSig] int GetSlideshowOptions(out uint options, out uint slideshowTick);

    [PreserveSig] int AdvanceSlideshow(
        [MarshalAs(UnmanagedType.LPWStr)] string monitorID,
        int direction);

    [PreserveSig] int GetStatus(out uint state);
    [PreserveSig] int Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
}
