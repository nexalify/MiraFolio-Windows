using MiraFolio.Core.Models;

namespace MiraFolio.Core.Services;

public interface IMonitorService
{
    IReadOnlyList<MonitorInfo> GetMonitors();
    event EventHandler MonitorsChanged;
}
