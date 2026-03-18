using System.Collections.Concurrent;
using System.Windows.Forms;

namespace SekoDL.Services;

public sealed class ClipboardMonitorService : IDisposable
{
    private readonly UrlValidationService _urlValidationService;
    private readonly ConcurrentDictionary<string, byte> _seenNormalizedUrls = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cancellationTokenSource;
    private Thread? _workerThread;
    private string? _lastClipboardText;

    public ClipboardMonitorService(UrlValidationService urlValidationService)
    {
        _urlValidationService = urlValidationService;
    }

    public event EventHandler<string>? ValidUrlDetected;

    public bool IsRunning => _workerThread?.IsAlive == true;

    public void Start(int pollIntervalMs)
    {
        if (IsRunning)
        {
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _workerThread = new Thread(() => PollClipboardLoop(Math.Max(250, pollIntervalMs), _cancellationTokenSource.Token))
        {
            IsBackground = true,
            Name = "SekoDL Clipboard Monitor"
        };
        _workerThread.SetApartmentState(ApartmentState.STA);
        _workerThread.Start();
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        _cancellationTokenSource?.Cancel();
        _workerThread?.Join(TimeSpan.FromSeconds(2));
        _workerThread = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    private void PollClipboardLoop(int pollIntervalMs, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    var currentText = Clipboard.GetText()?.Trim();
                    if (!string.IsNullOrWhiteSpace(currentText) &&
                        !string.Equals(currentText, _lastClipboardText, StringComparison.Ordinal))
                    {
                        _lastClipboardText = currentText;

                        if (_urlValidationService.TryNormalizeSupportedHttpUrl(currentText, out var normalizedUrl, out _) &&
                            _seenNormalizedUrls.TryAdd(normalizedUrl, 0))
                        {
                            ValidUrlDetected?.Invoke(this, normalizedUrl);
                        }
                    }
                }
            }
            catch
            {
            }

            cancellationToken.WaitHandle.WaitOne(pollIntervalMs);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
