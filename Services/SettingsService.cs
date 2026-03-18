using System.Text.Json;
using SekoDL.Models;

namespace SekoDL.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;
    private readonly string _defaultDownloadFolder;
    private readonly string _legacyAppDownloadFolder;

    public SettingsService(string dataDirectory, string defaultDownloadFolder)
    {
        Directory.CreateDirectory(dataDirectory);
        _settingsPath = Path.Combine(dataDirectory, "settings.json");
        _defaultDownloadFolder = defaultDownloadFolder;
        var appRoot = Directory.GetParent(dataDirectory)?.FullName ?? AppContext.BaseDirectory;
        _legacyAppDownloadFolder = Path.Combine(appRoot, "Downloads");
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            var defaults = CreateDefaultSettings();
            await SaveAsync(defaults);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream);
            return NormalizeSettings(settings ?? CreateDefaultSettings());
        }
        catch
        {
            var defaults = CreateDefaultSettings();
            await SaveAsync(defaults);
            return defaults;
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        settings = NormalizeSettings(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        await using var stream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }

    private AppSettings CreateDefaultSettings()
    {
        return new AppSettings
        {
            DefaultDownloadFolder = _defaultDownloadFolder,
            RefreshIntervalMs = 300,
            EnableClipboardMonitor = false,
            ClipboardPollIntervalMs = 1000,
            AutoImportExternalRequests = true,
            ExternalInboxFilePath = "Data/inbox.jsonl"
        };
    }

    private AppSettings NormalizeSettings(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DefaultDownloadFolder) ||
            string.Equals(settings.DefaultDownloadFolder, _legacyAppDownloadFolder, StringComparison.OrdinalIgnoreCase))
        {
            settings.DefaultDownloadFolder = _defaultDownloadFolder;
        }

        if (settings.RefreshIntervalMs < 100)
        {
            settings.RefreshIntervalMs = 300;
        }

        if (settings.ClipboardPollIntervalMs < 250)
        {
            settings.ClipboardPollIntervalMs = 1000;
        }

        if (string.IsNullOrWhiteSpace(settings.ExternalInboxFilePath))
        {
            settings.ExternalInboxFilePath = "Data/inbox.jsonl";
        }

        return settings;
    }
}
