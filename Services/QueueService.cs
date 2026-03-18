using SekoDL.Models;

namespace SekoDL.Services;

public sealed class QueueService : IAsyncDisposable
{
    private readonly DownloadService _downloadService;
    private readonly HistoryService _historyService;
    private readonly List<DownloadTaskInfo> _tasks = [];
    private readonly object _sync = new();
    private readonly CancellationTokenSource _workerCancellation = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly Task _workerTask;
    private DownloadTaskInfo? _activeTask;
    private CancellationTokenSource? _activeDownloadCancellation;
    private QueueCommand _pendingCommand = QueueCommand.None;

    public QueueService(DownloadService downloadService, HistoryService historyService)
    {
        _downloadService = downloadService;
        _historyService = historyService;
        _workerTask = Task.Run(ProcessQueueAsync);
    }

    public void Enqueue(DownloadTaskInfo taskInfo)
    {
        lock (_sync)
        {
            taskInfo.Status = "Queued";
            taskInfo.ErrorMessage = null;
            taskInfo.CompletedAt = null;

            if (_tasks.All(existing => existing.Id != taskInfo.Id))
            {
                _tasks.Add(taskInfo);
            }
        }

        _ = _historyService.AddOrUpdateAsync(taskInfo);
        _signal.Release();
    }

    public IReadOnlyList<DownloadTaskInfo> GetAllTasks()
    {
        lock (_sync)
        {
            return _tasks
                .OrderBy(task => task.CreatedAt)
                .ToList();
        }
    }

    public bool ContainsUrl(string url)
    {
        lock (_sync)
        {
            return _tasks.Any(task => string.Equals(task.Url, url, StringComparison.OrdinalIgnoreCase));
        }
    }

    public QueueActionResult PauseTask(Guid? taskId)
    {
        if (!taskId.HasValue)
        {
            return QueueActionResult.Fail("No download was selected.");
        }

        lock (_sync)
        {
            if (_activeTask?.Id != taskId.Value)
            {
                return QueueActionResult.Fail("Only the active download can be paused.");
            }

            if (!_activeTask.SupportsResume)
            {
                return QueueActionResult.Fail("Pause is unavailable because the server does not support resume.");
            }

            _pendingCommand = QueueCommand.Pause;
            _activeDownloadCancellation?.Cancel();
        }

        return QueueActionResult.Ok("Pause requested. The download will stop safely.");
    }

    public QueueActionResult ResumeTask(Guid? taskId)
    {
        if (!taskId.HasValue)
        {
            return QueueActionResult.Fail("No download was selected.");
        }

        lock (_sync)
        {
            var task = _tasks.FirstOrDefault(item => item.Id == taskId.Value);
            if (task is null)
            {
                return QueueActionResult.Fail("The selected download could not be found.");
            }

            if (!string.Equals(task.Status, "Paused", StringComparison.OrdinalIgnoreCase))
            {
                return QueueActionResult.Fail("Only paused downloads can be resumed.");
            }

            task.Status = "Queued";
            task.ErrorMessage = null;
            task.BytesPerSecond = 0;
            task.Eta = null;
        }

        _ = _historyService.AddOrUpdateAsync(GetTask(taskId.Value)!);
        _signal.Release();
        return QueueActionResult.Ok("Download queued for resume.");
    }

    public QueueActionResult CancelTask(Guid? taskId, bool deletePartial)
    {
        if (!taskId.HasValue)
        {
            return QueueActionResult.Fail("No download was selected.");
        }

        DownloadTaskInfo? task;
        var isActive = false;

        lock (_sync)
        {
            task = _tasks.FirstOrDefault(item => item.Id == taskId.Value);
            if (task is null)
            {
                return QueueActionResult.Fail("The selected download could not be found.");
            }

            isActive = _activeTask?.Id == task.Id;
            if (isActive)
            {
                _pendingCommand = deletePartial ? QueueCommand.CancelAndDeletePartial : QueueCommand.CancelKeepPartial;
                _activeDownloadCancellation?.Cancel();
            }
            else
            {
                task.Status = "Cancelled";
                task.CompletedAt = DateTime.Now;
                task.ErrorMessage = deletePartial ? "Cancelled by user." : "Cancelled by user. Partial file kept.";
                task.BytesPerSecond = 0;
                task.Eta = null;

                if (deletePartial && File.Exists(task.TempFilePath))
                {
                    File.Delete(task.TempFilePath);
                }
            }
        }

        if (task is not null && !isActive)
        {
            _ = _historyService.AddOrUpdateAsync(task);
        }

        return QueueActionResult.Ok("Cancel requested.");
    }

    public async Task StopAsync()
    {
        if (_workerCancellation.IsCancellationRequested)
        {
            return;
        }

        _workerCancellation.Cancel();
        lock (_sync)
        {
            _activeDownloadCancellation?.Cancel();
        }

        _signal.Release();

        try
        {
            await _workerTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessQueueAsync()
    {
        while (!_workerCancellation.IsCancellationRequested)
        {
            var nextTask = GetNextQueuedTask();
            if (nextTask is null)
            {
                try
                {
                    await _signal.WaitAsync(_workerCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                continue;
            }

            CancellationTokenSource? downloadCancellation = null;
            try
            {
                lock (_sync)
                {
                    _activeTask = nextTask;
                    _pendingCommand = QueueCommand.None;
                    nextTask.Status = "Connecting";
                    nextTask.ErrorMessage = null;
                    downloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(_workerCancellation.Token);
                    _activeDownloadCancellation = downloadCancellation;
                }

                await _historyService.AddOrUpdateAsync(nextTask);

                lock (_sync)
                {
                    nextTask.Status = "Downloading";
                }

                await _downloadService.DownloadAsync(nextTask, OnTaskProgress, downloadCancellation.Token);

                lock (_sync)
                {
                    nextTask.Status = "Completed";
                    nextTask.CompletedAt = DateTime.Now;
                    nextTask.BytesPerSecond = 0;
                    nextTask.Eta = TimeSpan.Zero;
                }

                await _historyService.AddOrUpdateAsync(nextTask);
            }
            catch (OperationCanceledException) when (_workerCancellation.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                await ApplyCancellationResultAsync(nextTask);
            }
            catch (ResumeNotSupportedException ex)
            {
                lock (_sync)
                {
                    nextTask.Status = "Paused";
                    nextTask.ErrorMessage = ex.Message;
                    nextTask.BytesPerSecond = 0;
                    nextTask.Eta = null;
                }

                await _historyService.AddOrUpdateAsync(nextTask);
            }
            catch (Exception ex)
            {
                lock (_sync)
                {
                    nextTask.Status = "Failed";
                    nextTask.CompletedAt = DateTime.Now;
                    nextTask.ErrorMessage = ex.Message;
                    nextTask.BytesPerSecond = 0;
                    nextTask.Eta = null;
                }

                await _historyService.AddOrUpdateAsync(nextTask);
            }
            finally
            {
                lock (_sync)
                {
                    downloadCancellation?.Dispose();
                    _activeDownloadCancellation = null;
                    _activeTask = null;
                    _pendingCommand = QueueCommand.None;
                }
            }
        }
    }

    private void OnTaskProgress(DownloadTaskInfo taskInfo)
    {
        _ = _historyService.AddOrUpdateAsync(taskInfo);
    }

    private async Task ApplyCancellationResultAsync(DownloadTaskInfo taskInfo)
    {
        QueueCommand command;
        lock (_sync)
        {
            command = _pendingCommand;
            taskInfo.CompletedAt = DateTime.Now;
            taskInfo.BytesPerSecond = 0;
            taskInfo.Eta = null;

            switch (command)
            {
                case QueueCommand.Pause:
                    taskInfo.Status = "Paused";
                    taskInfo.ErrorMessage = null;
                    taskInfo.CompletedAt = null;
                    break;

                case QueueCommand.CancelAndDeletePartial:
                    taskInfo.Status = "Cancelled";
                    taskInfo.ErrorMessage = "Cancelled by user.";
                    break;

                case QueueCommand.CancelKeepPartial:
                    taskInfo.Status = "Cancelled";
                    taskInfo.ErrorMessage = "Cancelled by user. Partial file kept.";
                    break;

                default:
                    taskInfo.Status = "Failed";
                    taskInfo.ErrorMessage = "The download stopped unexpectedly.";
                    break;
            }
        }

        if (command == QueueCommand.CancelAndDeletePartial && File.Exists(taskInfo.TempFilePath))
        {
            File.Delete(taskInfo.TempFilePath);
        }

        await _historyService.AddOrUpdateAsync(taskInfo);
    }

    private DownloadTaskInfo? GetNextQueuedTask()
    {
        lock (_sync)
        {
            return _tasks.FirstOrDefault(task => string.Equals(task.Status, "Queued", StringComparison.OrdinalIgnoreCase));
        }
    }

    private DownloadTaskInfo? GetTask(Guid id)
    {
        lock (_sync)
        {
            return _tasks.FirstOrDefault(task => task.Id == id);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _signal.Dispose();
        _workerCancellation.Dispose();
    }
}

public enum QueueCommand
{
    None,
    Pause,
    CancelKeepPartial,
    CancelAndDeletePartial
}

public sealed record QueueActionResult(bool Success, string Message)
{
    public static QueueActionResult Ok(string message) => new(true, message);

    public static QueueActionResult Fail(string message) => new(false, message);
}
