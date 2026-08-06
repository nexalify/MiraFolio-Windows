using MiraFolio.Core.Models;

namespace MiraFolio.Core.Utilities;

internal static class MonitorTopologyFilter
{
    public static IReadOnlyList<MonitorInfo> FilterToActiveDesktop(
        IReadOnlyList<MonitorCandidate> candidates,
        IReadOnlyCollection<MonitorBounds> activeBounds)
    {
        if (candidates.Count == 0)
            return Array.Empty<MonitorInfo>();

        if (activeBounds.Count == 0)
            return Reindex(candidates);

        var activeBoundsSet = activeBounds.ToHashSet();
        var usedBounds = new HashSet<MonitorBounds>();
        var filtered = new List<MonitorInfo>(Math.Min(candidates.Count, activeBoundsSet.Count));

        foreach (var candidate in candidates)
        {
            if (!activeBoundsSet.Contains(candidate.Bounds))
                continue;

            if (!usedBounds.Add(candidate.Bounds))
                continue;

            filtered.Add(candidate.ToMonitorInfo(filtered.Count));
        }

        return filtered.Count > 0 ? filtered : Reindex(candidates);
    }

    private static IReadOnlyList<MonitorInfo> Reindex(IReadOnlyList<MonitorCandidate> candidates) =>
        candidates.Select((candidate, index) => candidate.ToMonitorInfo(index)).ToArray();
}

internal readonly record struct MonitorCandidate(string DevicePath, MonitorBounds Bounds)
{
    public MonitorInfo ToMonitorInfo(int index) =>
        new(index, DevicePath, Bounds.Left, Bounds.Top, Bounds.Width, Bounds.Height);
}

internal readonly record struct MonitorBounds(int Left, int Top, int Width, int Height);
