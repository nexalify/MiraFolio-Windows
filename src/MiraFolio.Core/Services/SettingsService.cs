using System.Text.Json;
using Microsoft.Extensions.Logging;
using MiraFolio.Core.Models;
using MiraFolio.Core.Utilities;

namespace MiraFolio.Core.Services;

public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsPath;
    private readonly string _statePath;
    private readonly object _settingsLock = new();
    private readonly object _stateLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService(ILogger<SettingsService> logger)
        : this(logger, AppDataPaths.CurrentDirectory)
    {
    }

    internal SettingsService(
        ILogger<SettingsService> logger,
        string appDataDirectory)
    {
        _logger = logger;
        Directory.CreateDirectory(appDataDirectory);
        _settingsPath = Path.Combine(appDataDirectory, "settings.json");
        _statePath = Path.Combine(appDataDirectory, "state.json");
    }

    public AppSettings Load()
    {
        lock (_settingsLock)
        {
            try
            {
                return LoadFromFile<AppSettings>(_settingsPath) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load settings, using defaults");
                return new AppSettings();
            }
        }
    }

    public void Save(AppSettings settings, bool notifyChanged = true)
    {
        try
        {
            lock (_settingsLock)
                WriteAtomically(_settingsPath, settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            return;
        }

        if (notifyChanged)
            NotifySettingsChanged(settings);
    }

    public RuntimeState LoadState()
    {
        lock (_stateLock)
        {
            try
            {
                return LoadFromFile<RuntimeState>(_statePath) ?? new RuntimeState();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load runtime state");
                return new RuntimeState();
            }
        }
    }

    public void UpdateState(Action<RuntimeState> update)
    {
        try
        {
            lock (_stateLock)
            {
                var state = LoadFromFile<RuntimeState>(_statePath) ?? new RuntimeState();
                update(state);
                WriteAtomically(_statePath, state);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update runtime state");
        }
    }

    private static T? LoadFromFile<T>(string path)
    {
        if (!File.Exists(path))
            return default;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static void WriteAtomically<T>(string path, T value)
    {
        var tempPath = path + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private void NotifySettingsChanged(AppSettings settings)
    {
        var handlers = SettingsChanged;
        if (handlers == null)
            return;

        foreach (EventHandler<AppSettings> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A settings change subscriber failed");
            }
        }
    }
}
