namespace SekoDL.Utils;

public static class TimeFormatter
{
    public static string FormatEta(TimeSpan? eta)
    {
        if (!eta.HasValue)
        {
            return "-";
        }

        return eta.Value.TotalHours >= 1
            ? eta.Value.ToString(@"hh\:mm\:ss")
            : eta.Value.ToString(@"mm\:ss");
    }

    public static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"hh\:mm\:ss")
            : duration.ToString(@"mm\:ss");
    }
}
