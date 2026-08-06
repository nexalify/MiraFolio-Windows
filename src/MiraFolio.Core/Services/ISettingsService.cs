using MiraFolio.Core.Models;

namespace MiraFolio.Core.Services;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings, bool notifyChanged = true);
    RuntimeState LoadState();
    void UpdateState(Action<RuntimeState> update);
    event EventHandler<AppSettings> SettingsChanged;
}
