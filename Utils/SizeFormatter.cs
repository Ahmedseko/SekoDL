namespace SekoDL.Utils;

public static class SizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long bytes) => Format((double)bytes);

    public static string Format(double bytes)
    {
        var value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < Units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {Units[unitIndex]}";
    }
}
