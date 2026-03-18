using Spectre.Console;
using Color = Spectre.Console.Color;

namespace SekoDL.UI;

public static class Theme
{
    public static Style TitleStyle => new(Color.Aqua, Color.Black, Decoration.Bold);
    public static Style InfoStyle => new(Color.Cyan1);
    public static Style SuccessStyle => new(Color.Green1);
    public static Style WarningStyle => new(Color.Yellow1);
    public static Style ErrorStyle => new(Color.Red1);
    public static Style QueuedStyle => new(Color.Grey70);

    public static string StatusMarkup(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "downloading" => "[black on aqua] DOWNLOADING [/]",
            "connecting" => "[black on deepskyblue1] CONNECTING [/]",
            "completed" => "[black on green3_1] COMPLETED [/]",
            "paused" => "[black on yellow1] PAUSED [/]",
            "failed" => "[white on red1] FAILED [/]",
            "cancelled" => "[black on darkorange3_1] CANCELLED [/]",
            "queued" => "[black on grey70] QUEUED [/]",
            _ => $"[white on grey35] {Markup.Escape(status.ToUpperInvariant())} [/]"
        };
    }
}
