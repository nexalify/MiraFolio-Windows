using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using MiraFolio.Core.Interop;

namespace MiraFolio.Core.Services;

/// <summary>
/// Owns the IDesktopWallpaper COM object and its STA dispatcher for the full app lifetime.
/// </summary>
public sealed class DesktopWallpaperHost : IDisposable
{
    private readonly ILogger<DesktopWallpaperHost> _logger;
    private readonly Thread _staThread;
    private readonly ManualResetEventSlim _ready = new(false);
    private Dispatcher? _dispatcher;
    private IDesktopWallpaper? _desktopWallpaper;
    private Exception? _initializationError;
    private bool _disposed;

    public DesktopWallpaperHost(ILogger<DesktopWallpaperHost> logger)
    {
        _logger = logger;
        _staThread = new Thread(RunStaThread)
        {
            IsBackground = true,
            Name = "MiraFolio.DesktopWallpaper"
        };
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.Start();

        if (!_ready.Wait(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("Timed out initializing the desktop wallpaper COM service.");

        if (_initializationError != null)
            throw new InvalidOperationException("Failed to initialize the desktop wallpaper COM service.", _initializationError);
    }

    internal void Invoke(Action<IDesktopWallpaper> action)
    {
        ThrowIfUnavailable();
        _dispatcher!.Invoke(() => action(_desktopWallpaper!));
    }

    internal T Invoke<T>(Func<IDesktopWallpaper, T> action)
    {
        ThrowIfUnavailable();
        return _dispatcher!.Invoke(() => action(_desktopWallpaper!));
    }

    private void RunStaThread()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        try
        {
            var clsid = new Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD");
            var comType = Type.GetTypeFromCLSID(clsid)
                ?? throw new COMException("IDesktopWallpaper COM type is unavailable.");
            _desktopWallpaper = Activator.CreateInstance(comType) as IDesktopWallpaper
                ?? throw new COMException("Created COM object does not implement IDesktopWallpaper.");
            _logger.LogInformation("IDesktopWallpaper COM initialized successfully");
        }
        catch (Exception ex)
        {
            _initializationError = ex;
            _logger.LogError(ex, "Failed to initialize IDesktopWallpaper COM object");
        }
        finally
        {
            _ready.Set();
        }

        if (_initializationError != null)
            return;

        try
        {
            Dispatcher.Run();
        }
        finally
        {
            if (_desktopWallpaper != null)
            {
                Marshal.FinalReleaseComObject(_desktopWallpaper);
                _desktopWallpaper = null;
            }
        }
    }

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initializationError != null || _dispatcher == null || _desktopWallpaper == null)
            throw new InvalidOperationException("The desktop wallpaper COM service is unavailable.", _initializationError);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _dispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
        if (!_staThread.Join(TimeSpan.FromSeconds(5)))
            _logger.LogWarning("Desktop wallpaper STA thread did not stop within the timeout");
        _ready.Dispose();
    }
}
