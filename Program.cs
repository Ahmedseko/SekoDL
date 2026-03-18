using System.Collections.Concurrent;
using SekoDL.Models;
using SekoDL.Services;
using SekoDL.UI;
using Spectre.Console;

namespace SekoDL;

internal static class Program
{
    [STAThread]
    private static async Task Main(string[] args)
    {
        var appRoot = AppContext.BaseDirectory;
        var dataDirectory = Path.Combine(appRoot, "Data");
        var downloadsDirectory = GetDefaultDownloadsDirectory();

        Directory.CreateDirectory(dataDirectory);
        Directory.CreateDirectory(downloadsDirectory);

        var urlValidationService = new UrlValidationService();
        var protocolHandlerService = new ProtocolHandlerService(urlValidationService);

        if (args.Length == 1 && string.Equals(args[0], "--native-host", StringComparison.OrdinalIgnoreCase))
        {
            var nativeSettings = new SettingsService(dataDirectory, downloadsDirectory);
            var loadedNativeSettings = await nativeSettings.LoadAsync();
            var nativeInboxService = new ExternalInboxService(appRoot, loadedNativeSettings.ExternalInboxFilePath, urlValidationService);
            var nativeHostService = new NativeMessagingHostService(urlValidationService, nativeInboxService);
            await nativeHostService.RunAsync(CancellationToken.None);
            return;
        }

        var startupProtocolUrl = TryGetStartupProtocolUrl(args, protocolHandlerService, out var startupProtocolError);

        var settingsService = new SettingsService(dataDirectory, downloadsDirectory);
        var settings = await settingsService.LoadAsync();

        Directory.CreateDirectory(settings.DefaultDownloadFolder);

        var historyService = new HistoryService(dataDirectory);
        await historyService.LoadAsync();

        var externalInboxService = new ExternalInboxService(appRoot, settings.ExternalInboxFilePath, urlValidationService);

        using var downloadService = new DownloadService();
        using var speedTestService = new SpeedTestService();
        await using var queueService = new QueueService(downloadService, historyService);
        using var clipboardMonitorService = new ClipboardMonitorService(urlValidationService);

        var ui = new TerminalUi();
        var dashboard = new DownloadDashboard();
        var pendingClipboardUrls = new ConcurrentQueue<string>();

        clipboardMonitorService.ValidUrlDetected += (_, url) => pendingClipboardUrls.Enqueue(url);

        ui.ShowBanner();

        if (!string.IsNullOrWhiteSpace(startupProtocolError))
        {
            ui.ShowWarning(startupProtocolError);
        }

        if (settings.AutoImportExternalRequests)
        {
            await ImportExternalRequestsAsync(ui, downloadService, queueService, externalInboxService, settings);
        }

        if (!string.IsNullOrWhiteSpace(startupProtocolUrl))
        {
            var result = await EnqueueExternalUrlAsync(
                startupProtocolUrl,
                "Custom protocol",
                downloadService,
                queueService,
                settings.DefaultDownloadFolder);

            HandleActionResult(ui, result);
        }

        if (settings.EnableClipboardMonitor)
        {
            clipboardMonitorService.Start(settings.ClipboardPollIntervalMs);
            ui.ShowSuccess("Clipboard monitor is ON.");
        }

        var exitRequested = false;
        while (!exitRequested)
        {
            await ProcessPendingClipboardUrlsAsync(ui, pendingClipboardUrls, downloadService, queueService, settings);

            var choice = ui.ShowMainMenu(settings);

            switch (choice)
            {
                case MainMenuOption.AddDownload:
                    await AddDownloadAsync(ui, downloadService, queueService, settings);
                    break;

                case MainMenuOption.ViewActiveDownloads:
                    await dashboard.ShowAsync(queueService, settings.RefreshIntervalMs);
                    break;

                case MainMenuOption.PauseDownload:
                    HandleActionResult(ui, queueService.PauseTask(ui.PromptTaskSelection(queueService.GetAllTasks(), "Select a download to pause.")));
                    break;

                case MainMenuOption.ResumeDownload:
                    HandleActionResult(ui, queueService.ResumeTask(ui.PromptTaskSelection(queueService.GetAllTasks(), "Select a download to resume.")));
                    break;

                case MainMenuOption.CancelDownload:
                    {
                        var taskId = ui.PromptTaskSelection(queueService.GetAllTasks(), "Select a download to cancel.");
                        var deletePartial = taskId.HasValue && ui.ConfirmDeletePartial();
                        HandleActionResult(ui, queueService.CancelTask(taskId, deletePartial));
                    }
                    break;

                case MainMenuOption.ViewHistory:
                    ui.ShowHistory(historyService.GetAll());
                    break;

                case MainMenuOption.Settings:
                    {
                        var updatedSettings = await ui.EditSettingsAsync(settings, settingsService);
                        if (updatedSettings is not null)
                        {
                            settings = updatedSettings;
                            Directory.CreateDirectory(settings.DefaultDownloadFolder);
                            externalInboxService = new ExternalInboxService(appRoot, settings.ExternalInboxFilePath, urlValidationService);
                        }
                    }
                    break;

                case MainMenuOption.ToggleClipboardMonitor:
                    settings = await ToggleClipboardMonitorAsync(settings, settingsService, clipboardMonitorService, ui);
                    break;

                case MainMenuOption.ImportPendingExternalRequests:
                    await ImportExternalRequestsAsync(ui, downloadService, queueService, externalInboxService, settings);
                    break;

                case MainMenuOption.InternetSpeedTest:
                    await RunInternetSpeedTestAsync(ui, speedTestService);
                    break;

                case MainMenuOption.IntegrationHelp:
                    ui.ShowIntegrationHelp(
                        Path.Combine(appRoot, "BrowserExtension", "chrome-edge"),
                        Path.Combine(appRoot, "BrowserExtension", "firefox"),
                        Path.Combine(appRoot, "Install"));
                    break;

                case MainMenuOption.Exit:
                    exitRequested = true;
                    break;
            }
        }

        clipboardMonitorService.Stop();
        await queueService.StopAsync();
    }

    private static async Task AddDownloadAsync(
        TerminalUi ui,
        DownloadService downloadService,
        QueueService queueService,
        AppSettings settings)
    {
        var request = ui.PromptAddDownload(settings.DefaultDownloadFolder);
        if (request is null)
        {
            return;
        }

        var result = await EnqueueExternalUrlAsync(
            request.Url,
            "Manual input",
            downloadService,
            queueService,
            request.SaveDirectory);

        HandleActionResult(ui, result);
    }

    private static string? TryGetStartupProtocolUrl(
        string[] args,
        ProtocolHandlerService protocolHandlerService,
        out string? error)
    {
        error = null;
        if (args.Length != 1 || !args[0].StartsWith("sekodl://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (protocolHandlerService.TryParseDownloadUrl(args[0], out var normalizedUrl, out var parseError))
        {
            return normalizedUrl;
        }

        error = $"Protocol request ignored: {parseError}";
        return null;
    }

    private static async Task<QueueActionResult> EnqueueExternalUrlAsync(
        string url,
        string source,
        DownloadService downloadService,
        QueueService queueService,
        string saveDirectory)
    {
        if (queueService.ContainsUrl(url))
        {
            return QueueActionResult.Fail($"{source}: URL already exists in the queue.");
        }

        try
        {
            var taskInfo = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Theme.InfoStyle)
                .StartAsync($"Preparing {source.ToLowerInvariant()}...", async _ =>
                    await downloadService.PrepareDownloadAsync(url, saveDirectory, CancellationToken.None));

            queueService.Enqueue(taskInfo);
            return QueueActionResult.Ok($"{source}: queued {taskInfo.FileName} ({taskInfo.SupportsResumeMessage}).");
        }
        catch (Exception ex)
        {
            return QueueActionResult.Fail($"{source}: {ex.Message}");
        }
    }

    private static async Task<AppSettings> ToggleClipboardMonitorAsync(
        AppSettings settings,
        SettingsService settingsService,
        ClipboardMonitorService clipboardMonitorService,
        TerminalUi ui)
    {
        settings.EnableClipboardMonitor = !settings.EnableClipboardMonitor;
        await settingsService.SaveAsync(settings);

        if (settings.EnableClipboardMonitor)
        {
            clipboardMonitorService.Start(settings.ClipboardPollIntervalMs);
            ui.ShowSuccess("Clipboard monitor enabled.");
        }
        else
        {
            clipboardMonitorService.Stop();
            ui.ShowWarning("Clipboard monitor disabled.");
        }

        return settings;
    }

    private static async Task ProcessPendingClipboardUrlsAsync(
        TerminalUi ui,
        ConcurrentQueue<string> pendingClipboardUrls,
        DownloadService downloadService,
        QueueService queueService,
        AppSettings settings)
    {
        while (pendingClipboardUrls.TryDequeue(out var url))
        {
            if (!ui.ConfirmEnqueueExternalUrl(url, "Clipboard"))
            {
                continue;
            }

            var result = await EnqueueExternalUrlAsync(
                url,
                "Clipboard",
                downloadService,
                queueService,
                settings.DefaultDownloadFolder);

            HandleActionResult(ui, result);
        }
    }

    private static async Task ImportExternalRequestsAsync(
        TerminalUi ui,
        DownloadService downloadService,
        QueueService queueService,
        ExternalInboxService externalInboxService,
        AppSettings settings)
    {
        var importResult = await externalInboxService.ImportPendingAsync();
        var importedCount = 0;
        var skippedCount = importResult.SkippedCount;

        foreach (var url in importResult.ValidUrls)
        {
            var enqueueResult = await EnqueueExternalUrlAsync(
                url,
                "Inbox import",
                downloadService,
                queueService,
                settings.DefaultDownloadFolder);

            if (enqueueResult.Success)
            {
                importedCount++;
            }
            else
            {
                skippedCount++;
            }
        }

        ui.ShowImportSummary(importedCount, skippedCount, importResult.InvalidCount, importResult.InboxPath);
    }

    private static void HandleActionResult(TerminalUi ui, QueueActionResult result)
    {
        if (result.Success)
        {
            ui.ShowSuccess(result.Message);
            return;
        }

        ui.ShowWarning(result.Message);
    }

    private static async Task RunInternetSpeedTestAsync(TerminalUi ui, SpeedTestService speedTestService)
    {
        try
        {
            var result = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Theme.InfoStyle)
                .StartAsync("Running internet speed test...", async _ =>
                    await speedTestService.RunAsync(CancellationToken.None));

            ui.ShowSpeedTestResult(result);
        }
        catch (Exception ex)
        {
            ui.ShowError($"Speed test failed: {ex.Message}");
        }
    }

    private static string GetDefaultDownloadsDirectory()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            return Path.Combine(userProfile, "Downloads");
        }

        return Path.Combine(AppContext.BaseDirectory, "Downloads");
    }
}
