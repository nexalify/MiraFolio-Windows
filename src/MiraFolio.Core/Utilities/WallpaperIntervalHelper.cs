using MiraFolio.Core.Models;

namespace MiraFolio.Core.Utilities;

public static class WallpaperIntervalHelper
{
    // System.Threading.Timer uses a 32-bit millisecond due time on Windows.
    private static readonly TimeSpan MaximumTimerInterval = TimeSpan.FromDays(49);

    public static (int Value, WallpaperIntervalUnit Unit) GetDisplayValue(WallpaperAssignment assignment)
    {
        if (assignment.IntervalValue.HasValue)
            return (Math.Max(1, assignment.IntervalValue.Value), assignment.IntervalUnit);

        return (Math.Max(1, assignment.IntervalMinutes ?? 30), WallpaperIntervalUnit.Minutes);
    }

    public static TimeSpan ToTimeSpan(WallpaperAssignment assignment, int fallbackMinutes)
    {
        double milliseconds;
        if (assignment.IntervalValue.HasValue)
        {
            var value = Math.Max(1, assignment.IntervalValue.Value);
            milliseconds = assignment.IntervalUnit switch
            {
                WallpaperIntervalUnit.Seconds => value * 1_000d,
                WallpaperIntervalUnit.Hours => value * 3_600_000d,
                _ => value * 60_000d
            };
        }
        else
        {
            milliseconds = Math.Max(1, assignment.IntervalMinutes ?? fallbackMinutes) * 60_000d;
        }

        return TimeSpan.FromMilliseconds(Math.Min(milliseconds, MaximumTimerInterval.TotalMilliseconds));
    }
}
