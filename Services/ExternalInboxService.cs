using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SekoDL.Models;

namespace SekoDL.Services;

public sealed class ExternalInboxService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly string _inboxPath;
    private readonly UrlValidationService _urlValidationService;
    private readonly string _mutexName;

    public ExternalInboxService(string appRoot, string configuredInboxPath, UrlValidationService urlValidationService)
    {
        _urlValidationService = urlValidationService;
        _inboxPath = Path.IsPathRooted(configuredInboxPath)
            ? configuredInboxPath
            : Path.Combine(appRoot, configuredInboxPath);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_inboxPath)));
        _mutexName = $"Global\\SekoDLInbox_{hash[..16]}";
    }

    public string InboxPath => _inboxPath;

    public async Task AppendRequestAsync(string url)
    {
        var entry = new NativeMessageRequest
        {
            Action = "enqueue",
            Url = url
        };

        var directory = Path.GetDirectoryName(_inboxPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var mutex = new Mutex(false, _mutexName);
        mutex.WaitOne();
        try
        {
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.AppendAllTextAsync(_inboxPath, json + Environment.NewLine);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    public Task<InboxImportResult> ImportPendingAsync()
    {
        if (!File.Exists(_inboxPath))
        {
            return Task.FromResult(new InboxImportResult(_inboxPath, [], 0, 0));
        }

        string[] lines;
        using (var mutex = new Mutex(false, _mutexName))
        {
            mutex.WaitOne();
            try
            {
                lines = File.ReadAllLines(_inboxPath);
                File.WriteAllText(_inboxPath, string.Empty);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        var validUrls = new List<string>();
        var dedupe = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skippedCount = 0;
        var invalidCount = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var request = JsonSerializer.Deserialize<NativeMessageRequest>(line);
                if (request is null || !string.Equals(request.Action, "enqueue", StringComparison.OrdinalIgnoreCase))
                {
                    skippedCount++;
                    continue;
                }

                if (!_urlValidationService.TryNormalizeSupportedHttpUrl(request.Url, out var normalizedUrl, out _))
                {
                    invalidCount++;
                    continue;
                }

                if (!dedupe.Add(normalizedUrl))
                {
                    skippedCount++;
                    continue;
                }

                validUrls.Add(normalizedUrl);
            }
            catch
            {
                invalidCount++;
            }
        }

        return Task.FromResult(new InboxImportResult(_inboxPath, validUrls, skippedCount, invalidCount));
    }
}

public sealed record InboxImportResult(
    string InboxPath,
    IReadOnlyList<string> ValidUrls,
    int SkippedCount,
    int InvalidCount);
