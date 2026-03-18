using SekoDL.Models;
using SekoDL.Services;
using SekoDL.Utils;
using Spectre.Console;
using Spectre.Console.Rendering;
using Panel = Spectre.Console.Panel;

namespace SekoDL.UI;

public sealed class DownloadDashboard
{
    public async Task ShowAsync(QueueService queueService, int refreshIntervalMs)
    {
        AnsiConsole.MarkupLine("[grey70]Watching downloads. Press Q or Esc to return to the menu.[/]");

        var shouldExit = false;
        await AnsiConsole.Live(BuildLayout(queueService.GetAllTasks()))
            .AutoClear(false)
            .Overflow(VerticalOverflow.Visible)
            .StartAsync(async context =>
            {
                while (!shouldExit)
                {
                    context.UpdateTarget(BuildLayout(queueService.GetAllTasks()));

                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true).Key;
                        if (key is ConsoleKey.Q or ConsoleKey.Escape)
                        {
                            shouldExit = true;
                        }
                    }

                    await Task.Delay(refreshIntervalMs);
                }
            });
    }

    private static IRenderable BuildLayout(IReadOnlyList<DownloadTaskInfo> tasks)
    {
        var activeTask = tasks.FirstOrDefault(task =>
            task.Status.Equals("Downloading", StringComparison.OrdinalIgnoreCase) ||
            task.Status.Equals("Connecting", StringComparison.OrdinalIgnoreCase));

        var header = new Panel(new Markup("[bold aqua]SekoDL[/]\n[grey70]Smart Console Download Manager[/]"))
        {
            Border = BoxBorder.Rounded
        };
        header.BorderStyle(Theme.InfoStyle);

        var body = activeTask is null
            ? new Panel("[grey70]No active download right now.[/]")
            : new Panel(BuildActiveTaskGrid(activeTask))
            {
                Header = new PanelHeader("[bold cyan]Active Download[/]"),
                Border = BoxBorder.Rounded
            };

        body.BorderStyle(Theme.InfoStyle);

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("File");
        table.AddColumn("Status");
        table.AddColumn("Downloaded");
        table.AddColumn("Speed");
        table.AddColumn("ETA");

        foreach (var task in tasks.DefaultIfEmpty())
        {
            if (task is null)
            {
                table.AddRow("[grey70]No downloads[/]", "[grey70]-[/]", "[grey70]-[/]", "[grey70]-[/]", "[grey70]-[/]");
                continue;
            }

            table.AddRow(
                Markup.Escape(task.FileName),
                Theme.StatusMarkup(task.Status),
                BuildBytesText(task),
                task.BytesPerSecond > 0 ? $"{SizeFormatter.Format(task.BytesPerSecond)}/s" : "[grey70]-[/]",
                task.Eta.HasValue ? TimeFormatter.FormatEta(task.Eta) : "[grey70]-[/]");
        }

        return new Rows(header, body, new Panel(table)
        {
            Header = new PanelHeader("[bold cyan]Queue[/]"),
            Border = BoxBorder.Rounded
        });
    }

    private static Grid BuildActiveTaskGrid(DownloadTaskInfo task)
    {
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow("[grey70]File[/]", Markup.Escape(task.FileName));
        grid.AddRow("[grey70]Status[/]", Theme.StatusMarkup(task.Status));
        grid.AddRow("[grey70]Downloaded[/]", BuildBytesText(task));
        grid.AddRow("[grey70]Speed[/]", task.BytesPerSecond > 0 ? $"{SizeFormatter.Format(task.BytesPerSecond)}/s" : "[grey70]-[/]");
        grid.AddRow("[grey70]ETA[/]", task.Eta.HasValue ? TimeFormatter.FormatEta(task.Eta) : "[grey70]-[/]");
        grid.AddRow("[grey70]Resume[/]", task.SupportsResume ? "[green]Supported[/]" : "[red]Unsupported[/]");
        grid.AddRow("[grey70]Progress[/]", BuildProgressBar(task));

        if (!string.IsNullOrWhiteSpace(task.ErrorMessage))
        {
            grid.AddRow("[grey70]Message[/]", $"[yellow]{Markup.Escape(task.ErrorMessage)}[/]");
        }

        return grid;
    }

    private static string BuildProgressBar(DownloadTaskInfo task)
    {
        if (!task.TotalBytes.HasValue || task.TotalBytes <= 0)
        {
            return "[deepskyblue1]Streaming[/] [grey70](size unknown)[/]";
        }

        var percent = Math.Clamp(task.DownloadedBytes / (double)task.TotalBytes.Value, 0, 1);
        const int width = 32;
        var filled = (int)Math.Round(percent * width);
        var bar = new string('=', filled).PadRight(width, '-');

        return $"[aqua][[{bar}]][/][white] {(percent * 100):0.0}%[/]";
    }

    private static string BuildBytesText(DownloadTaskInfo task)
    {
        return task.TotalBytes.HasValue
            ? $"{SizeFormatter.Format(task.DownloadedBytes)} / {SizeFormatter.Format(task.TotalBytes.Value)}"
            : $"{SizeFormatter.Format(task.DownloadedBytes)} / [grey70]Unknown[/]";
    }
}
