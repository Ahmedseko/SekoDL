using SekoDL.Models;
using SekoDL.Services;
using SekoDL.Utils;
using Spectre.Console;
using Color = Spectre.Console.Color;
using Padding = Spectre.Console.Padding;
using Panel = Spectre.Console.Panel;

namespace SekoDL.UI;

public sealed class TerminalUi
{
    public void ShowBanner()
    {
        AnsiConsole.Clear();

        var title = new FigletText("SekoDL")
            .LeftJustified()
            .Color(Color.Aqua);

        var panel = new Panel(new Rows(
            title,
            new Markup("[grey70]Smart Console Download Manager[/]"),
            new Markup("[deepskyblue1]Modern downloads inside the terminal[/]")))
        {
            Header = new PanelHeader("[bold cyan]SEKODL[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };

        panel.BorderStyle(Theme.InfoStyle);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public MainMenuOption ShowMainMenu(AppSettings settings)
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<MainMenuOption>()
                .Title("[bold]Main Menu[/]")
                .PageSize(10)
                .HighlightStyle(Theme.InfoStyle)
                .UseConverter(option => GetMenuLabel(option, settings))
                .AddChoices(Enum.GetValues<MainMenuOption>()));
    }

    public AddDownloadRequest? PromptAddDownload(string defaultFolder)
    {
        AnsiConsole.Write(new Rule("[cyan]Add Download[/]"));
        AnsiConsole.MarkupLine("[grey70]Type 'back' at any prompt to return to the main menu.[/]");
        AnsiConsole.MarkupLine($"[grey70]If the default folder is correct, choose 'Use current default folder' and press Enter.[/]");

        var url = AnsiConsole.Ask<string>("Enter download URL:");
        if (IsBackCommand(url) || string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var windowsDownloadsFolder = GetWindowsDownloadsFolder();
        var folderChoice = AnsiConsole.Prompt(
            new SelectionPrompt<FolderChoice>()
                .Title($"Save location [[current default: [grey]{Markup.Escape(defaultFolder)}[/]]]")
                .PageSize(4)
                .UseConverter(choice => choice switch
                {
                    FolderChoice.UseDefault => $"Use current default folder ({defaultFolder})",
                    FolderChoice.UseWindowsDownloads => $"Use Windows Downloads ({windowsDownloadsFolder})",
                    FolderChoice.EnterCustom => "Enter another folder manually",
                    FolderChoice.BackToMenu => "Back to main menu",
                    _ => choice.ToString()
                })
                .AddChoices(
                    FolderChoice.UseDefault,
                    FolderChoice.UseWindowsDownloads,
                    FolderChoice.EnterCustom,
                    FolderChoice.BackToMenu));

        if (folderChoice == FolderChoice.BackToMenu)
        {
            return null;
        }

        var saveFolder = folderChoice switch
        {
            FolderChoice.UseDefault => defaultFolder,
            FolderChoice.UseWindowsDownloads => windowsDownloadsFolder,
            FolderChoice.EnterCustom => PromptCustomFolder(defaultFolder),
            _ => defaultFolder
        };

        if (string.IsNullOrWhiteSpace(saveFolder))
        {
            return null;
        }

        return new AddDownloadRequest(url.Trim(), saveFolder.Trim());
    }

    public Guid? PromptTaskSelection(IReadOnlyList<DownloadTaskInfo> tasks, string prompt)
    {
        if (tasks.Count == 0)
        {
            ShowWarning("There are no downloads to select.");
            return null;
        }

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<DownloadTaskInfo>()
                .Title(prompt)
                .UseConverter(task =>
                    $"{task.FileName} | {task.Status} | {SizeFormatter.Format(task.DownloadedBytes)}")
                .PageSize(10)
                .AddChoices(tasks));

        return choice.Id;
    }

    public bool ConfirmDeletePartial()
    {
        return AnsiConsole.Confirm("Delete the partial .part file too?", false);
    }

    public void ShowHistory(IReadOnlyList<DownloadHistoryItem> history)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("File")
            .AddColumn("Status")
            .AddColumn("Downloaded")
            .AddColumn("Total")
            .AddColumn("Started")
            .AddColumn("Finished");

        if (history.Count == 0)
        {
            AnsiConsole.Write(new Panel("[grey70]No history yet.[/]") { Border = BoxBorder.Rounded });
            return;
        }

        foreach (var item in history)
        {
            table.AddRow(
                Markup.Escape(item.FileName),
                Theme.StatusMarkup(item.Status),
                SizeFormatter.Format(item.DownloadedBytes),
                item.TotalBytes.HasValue ? SizeFormatter.Format(item.TotalBytes.Value) : "[grey70]Unknown[/]",
                item.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                item.FinishedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "[grey70]-[/]");
        }

        AnsiConsole.Write(new Panel(table)
        {
            Header = new PanelHeader("[bold cyan]History[/]"),
            Border = BoxBorder.Rounded
        });
    }

    public void ShowSpeedTestResult(SpeedTestResult result)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.AddRow("Server", Markup.Escape(result.TestServer));
        table.AddRow("Latency", $"{result.AverageLatencyMs:0.0} ms");
        table.AddRow("Jitter", $"{result.JitterMs:0.0} ms");
        table.AddRow("Download", $"{result.DownloadMbps:0.00} Mbps");
        table.AddRow("Download", $"{result.DownloadMegabytesPerSecond:0.00} MB/s");
        table.AddRow("Data Used", SizeFormatter.Format(result.BytesDownloaded));
        table.AddRow("Duration", TimeFormatter.FormatDuration(result.TestDuration));

        AnsiConsole.Write(new Panel(table)
        {
            Header = new PanelHeader("[bold cyan]Internet Speed Test[/]"),
            Border = BoxBorder.Rounded
        });
    }

    public void ShowIntegrationHelp(string chromeEdgeExtensionFolder, string firefoxExtensionFolder, string installFolder)
    {
        var content = new Rows(
            new Markup("[bold cyan]Protocol example[/]"),
            new Markup("[grey70]sekodl://download?url=https%3A%2F%2Fexample.com%2Ffile.zip[/]"),
            new Markup(string.Empty),
            new Markup("[bold cyan]Example HTML button[/]"),
            new Markup("[grey70]<a href=\"sekodl://download?url=https%3A%2F%2Fexample.com%2Ffile.zip\">Download with SekoDL</a>[/]"),
            new Markup(string.Empty),
            new Markup("[bold cyan]Folders[/]"),
            new Markup($"[grey70]Chrome/Edge extension:[/] {Markup.Escape(chromeEdgeExtensionFolder)}"),
            new Markup($"[grey70]Firefox extension:[/] {Markup.Escape(firefoxExtensionFolder)}"),
            new Markup($"[grey70]Install scripts:[/] {Markup.Escape(installFolder)}"),
            new Markup(string.Empty),
            new Markup("[yellow]Only direct HTTP/HTTPS links are supported.[/]"),
            new Markup("[yellow]Protected or restricted media extraction is not supported.[/]"));

        var panel = new Panel(content)
        {
            Header = new PanelHeader("[bold cyan]Integration Help[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 1)
        };

        panel.BorderStyle(Theme.InfoStyle);
        AnsiConsole.Write(panel);
    }

    public void ShowImportSummary(int importedCount, int skippedCount, int invalidCount, string inboxPath)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Imported");
        table.AddColumn("Skipped");
        table.AddColumn("Invalid");
        table.AddColumn("Inbox");
        table.AddRow(
            $"[green]{importedCount}[/]",
            $"[yellow]{skippedCount}[/]",
            $"[red]{invalidCount}[/]",
            Markup.Escape(inboxPath));

        AnsiConsole.Write(new Panel(table)
        {
            Header = new PanelHeader("[bold cyan]External Import Summary[/]"),
            Border = BoxBorder.Rounded
        });
    }

    public bool ConfirmEnqueueExternalUrl(string url, string source)
    {
        var panel = new Panel(
            new Rows(
                new Markup($"[grey70]Source:[/] {Markup.Escape(source)}"),
                new Markup($"[grey70]URL:[/] {Markup.Escape(url)}"),
                new Markup("[yellow]Queue this direct link into SekoDL?[/]")))
        {
            Header = new PanelHeader("[bold cyan]External Link Detected[/]"),
            Border = BoxBorder.Rounded
        };

        panel.BorderStyle(Theme.InfoStyle);
        AnsiConsole.Write(panel);
        return AnsiConsole.Confirm("Add to queue now?", true);
    }

    public async Task<AppSettings?> EditSettingsAsync(AppSettings settings, SettingsService settingsService)
    {
        AnsiConsole.Write(new Rule("[cyan]Settings[/]"));
        AnsiConsole.MarkupLine("[grey70]Type 'back' at any prompt to return to the main menu without saving.[/]");
        AnsiConsole.MarkupLine("[grey70]Press Enter to keep the current folder, or type a new path to change it.[/]");

        var folder = AnsiConsole.Ask<string>(
            $"Default download folder [[current: [grey]{Markup.Escape(settings.DefaultDownloadFolder)}[/]]]:");

        if (IsBackCommand(folder))
        {
            ShowWarning("Settings update cancelled.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = settings.DefaultDownloadFolder;
        }

        var refresh = settings.RefreshIntervalMs;
        while (true)
        {
            var refreshInput = AnsiConsole.Ask<string>(
                $"Refresh interval ms [[current: [grey]{settings.RefreshIntervalMs}[/]]]:");

            if (IsBackCommand(refreshInput))
            {
                ShowWarning("Settings update cancelled.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(refreshInput))
            {
                break;
            }

            if (int.TryParse(refreshInput, out refresh) && refresh is >= 100 and <= 5000)
            {
                break;
            }

            ShowWarning("Use a number between 100 and 5000.");
        }

        var updated = new AppSettings
        {
            DefaultDownloadFolder = folder.Trim(),
            RefreshIntervalMs = refresh,
            EnableClipboardMonitor = settings.EnableClipboardMonitor,
            ClipboardPollIntervalMs = settings.ClipboardPollIntervalMs,
            AutoImportExternalRequests = settings.AutoImportExternalRequests,
            ExternalInboxFilePath = settings.ExternalInboxFilePath
        };

        await settingsService.SaveAsync(updated);
        ShowSuccess("Settings saved.");
        return updated;
    }

    public void ShowSuccess(string message)
    {
        AnsiConsole.MarkupLine($"[green]{Markup.Escape(message)}[/]");
    }

    public void ShowWarning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
    }

    public void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    }

    private static bool IsBackCommand(string value)
    {
        return string.Equals(value?.Trim(), "back", StringComparison.OrdinalIgnoreCase);
    }

    private string? PromptCustomFolder(string fallbackFolder)
    {
        while (true)
        {
            var customFolder = AnsiConsole.Ask<string>(
                $"Enter custom folder [[leave empty to use: [grey]{Markup.Escape(fallbackFolder)}[/]]]:");

            if (IsBackCommand(customFolder))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(customFolder))
            {
                return fallbackFolder;
            }

            return customFolder.Trim();
        }
    }

    private static string GetWindowsDownloadsFolder()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile)
            ? Path.Combine(AppContext.BaseDirectory, "Downloads")
            : Path.Combine(userProfile, "Downloads");
    }

    private static string GetMenuLabel(MainMenuOption option, AppSettings settings)
    {
        return option switch
        {
            MainMenuOption.AddDownload => "Add download",
            MainMenuOption.ViewActiveDownloads => "View active downloads",
            MainMenuOption.PauseDownload => "Pause download",
            MainMenuOption.ResumeDownload => "Resume download",
            MainMenuOption.CancelDownload => "Cancel download",
            MainMenuOption.ViewHistory => "View history",
            MainMenuOption.Settings => "Settings",
            MainMenuOption.ToggleClipboardMonitor => $"Toggle clipboard monitor ({(settings.EnableClipboardMonitor ? "ON" : "OFF")})",
            MainMenuOption.ImportPendingExternalRequests => "Import pending external requests",
            MainMenuOption.InternetSpeedTest => "Internet speed test",
            MainMenuOption.IntegrationHelp => "Integration help",
            MainMenuOption.Exit => "Exit",
            _ => option.ToString()
        };
    }
}

public enum MainMenuOption
{
    AddDownload,
    ViewActiveDownloads,
    PauseDownload,
    ResumeDownload,
    CancelDownload,
    ViewHistory,
    Settings,
    ToggleClipboardMonitor,
    ImportPendingExternalRequests,
    InternetSpeedTest,
    IntegrationHelp,
    Exit
}

public sealed record AddDownloadRequest(string Url, string SaveDirectory);

public enum FolderChoice
{
    UseDefault,
    UseWindowsDownloads,
    EnterCustom,
    BackToMenu
}
