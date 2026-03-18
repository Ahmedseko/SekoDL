using System.Text.Json;
using SekoDL.Models;

namespace SekoDL.Services;

public sealed class HistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _historyPath;
    private readonly List<DownloadHistoryItem> _items = [];
    private readonly object _sync = new();

    public HistoryService(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _historyPath = Path.Combine(dataDirectory, "history.json");
    }

    public async Task LoadAsync()
    {
        if (!File.Exists(_historyPath))
        {
            await SaveAsync();
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_historyPath);
            var items = await JsonSerializer.DeserializeAsync<List<DownloadHistoryItem>>(stream);
            lock (_sync)
            {
                _items.Clear();
                if (items is not null)
                {
                    _items.AddRange(items);
                }
            }
        }
        catch
        {
            lock (_sync)
            {
                _items.Clear();
            }

            await SaveAsync();
        }
    }

    public IReadOnlyList<DownloadHistoryItem> GetAll()
    {
        lock (_sync)
        {
            return _items
                .OrderByDescending(item => item.StartedAt)
                .ToList();
        }
    }

    public async Task AddOrUpdateAsync(DownloadTaskInfo taskInfo)
    {
        lock (_sync)
        {
            var existing = _items.FirstOrDefault(item => item.Id == taskInfo.Id);
            if (existing is null)
            {
                _items.Add(ToHistoryItem(taskInfo));
            }
            else
            {
                existing.Url = taskInfo.Url;
                existing.FileName = taskInfo.FileName;
                existing.FinalFilePath = taskInfo.FinalFilePath;
                existing.Status = taskInfo.Status;
                existing.DownloadedBytes = taskInfo.DownloadedBytes;
                existing.TotalBytes = taskInfo.TotalBytes;
                existing.StartedAt = taskInfo.CreatedAt;
                existing.FinishedAt = taskInfo.CompletedAt;
                existing.ErrorMessage = taskInfo.ErrorMessage;
            }
        }

        await SaveAsync();
    }

    private async Task SaveAsync()
    {
        List<DownloadHistoryItem> snapshot;
        lock (_sync)
        {
            snapshot = _items.ToList();
        }

        await using var stream = File.Create(_historyPath);
        await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions);
    }

    private static DownloadHistoryItem ToHistoryItem(DownloadTaskInfo taskInfo)
    {
        return new DownloadHistoryItem
        {
            Id = taskInfo.Id,
            Url = taskInfo.Url,
            FileName = taskInfo.FileName,
            FinalFilePath = taskInfo.FinalFilePath,
            Status = taskInfo.Status,
            DownloadedBytes = taskInfo.DownloadedBytes,
            TotalBytes = taskInfo.TotalBytes,
            StartedAt = taskInfo.CreatedAt,
            FinishedAt = taskInfo.CompletedAt,
            ErrorMessage = taskInfo.ErrorMessage
        };
    }
}
