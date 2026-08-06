using MiraFolio.Core.Models;

namespace MiraFolio.Core.Utilities;

public static class MonitorAssignmentMerger
{
    public static List<WallpaperAssignment> Merge(
        IEnumerable<WallpaperAssignment> existingAssignments,
        IEnumerable<WallpaperAssignment> currentAssignments)
    {
        var currentByDevicePath = currentAssignments.ToDictionary(
            assignment => assignment.MonitorDevicePath,
            assignment => assignment,
            StringComparer.OrdinalIgnoreCase);

        var merged = new List<WallpaperAssignment>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var existing in existingAssignments)
        {
            if (currentByDevicePath.TryGetValue(existing.MonitorDevicePath, out var current))
            {
                merged.Add(current);
                seen.Add(existing.MonitorDevicePath);
            }
            else
            {
                merged.Add(existing);
                seen.Add(existing.MonitorDevicePath);
            }
        }

        foreach (var current in currentAssignments)
        {
            if (seen.Add(current.MonitorDevicePath))
                merged.Add(current);
        }

        return merged;
    }
}
