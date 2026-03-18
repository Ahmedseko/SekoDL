using System.Diagnostics;
using SekoDL.Models;

namespace SekoDL.Services;

public sealed class SpeedTestService : IDisposable
{
    private const string TestServerName = "Cloudflare Speed";
    private const string LatencyUrl = "https://speed.cloudflare.com/__down?bytes=1";
    private const string DownloadUrl = "https://speed.cloudflare.com/__down?bytes=200000000";
    private readonly HttpClient _httpClient;

    public SpeedTestService()
    {
        _httpClient = new HttpClient(new SocketsHttpHandler())
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SekoDL/1.0");
    }

    public async Task<SpeedTestResult> RunAsync(CancellationToken cancellationToken)
    {
        var latencySamples = await MeasureLatencyAsync(cancellationToken);
        var downloadResult = await MeasureDownloadSpeedAsync(cancellationToken);

        return new SpeedTestResult
        {
            AverageLatencyMs = latencySamples.Average(),
            JitterMs = CalculateJitter(latencySamples),
            DownloadMbps = downloadResult.bitsPerSecond / 1_000_000d,
            DownloadMegabytesPerSecond = downloadResult.bytesPerSecond / 1_000_000d,
            BytesDownloaded = downloadResult.bytesDownloaded,
            TestDuration = downloadResult.duration,
            TestServer = TestServerName
        };
    }

    private async Task<IReadOnlyList<double>> MeasureLatencyAsync(CancellationToken cancellationToken)
    {
        var samples = new List<double>();

        for (var i = 0; i < 5; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatencyUrl);
            var stopwatch = Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        return samples;
    }

    private async Task<(long bytesDownloaded, double bytesPerSecond, double bitsPerSecond, TimeSpan duration)> MeasureDownloadSpeedAsync(CancellationToken cancellationToken)
    {
        using var timedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timedCancellation.CancelAfter(TimeSpan.FromSeconds(8));

        long totalBytesDownloaded = 0;
        var stopwatch = Stopwatch.StartNew();
        var workers = Enumerable.Range(0, 4)
            .Select(_ => DownloadWorkerAsync(bytesRead => Interlocked.Add(ref totalBytesDownloaded, bytesRead), timedCancellation.Token))
            .ToArray();

        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException) when (timedCancellation.IsCancellationRequested)
        {
        }

        stopwatch.Stop();

        var elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
        var bytesPerSecond = totalBytesDownloaded / elapsedSeconds;
        var bitsPerSecond = bytesPerSecond * 8;

        return (totalBytesDownloaded, bytesPerSecond, bitsPerSecond, stopwatch.Elapsed);
    }

    private async Task DownloadWorkerAsync(Action<int> onBytesRead, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{DownloadUrl}&seed={Guid.NewGuid():N}");
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[131072];

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            onBytesRead(read);
        }
    }

    private static double CalculateJitter(IReadOnlyList<double> latencySamples)
    {
        if (latencySamples.Count < 2)
        {
            return 0;
        }

        var diffs = new List<double>();
        for (var i = 1; i < latencySamples.Count; i++)
        {
            diffs.Add(Math.Abs(latencySamples[i] - latencySamples[i - 1]));
        }

        return diffs.Average();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
