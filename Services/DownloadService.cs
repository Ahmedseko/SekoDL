using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using SekoDL.Models;
using SekoDL.Utils;

namespace SekoDL.Services;

public sealed class DownloadService : IDisposable
{
    private readonly HttpClient _httpClient;

    public DownloadService()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SekoDL/1.0");
    }

    public async Task<DownloadTaskInfo> PrepareDownloadAsync(string url, string saveDirectory, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Please enter a valid HTTP or HTTPS URL.");
        }

        Directory.CreateDirectory(saveDirectory);

        HttpResponseMessage? response = null;
        try
        {
            response = await SendMetadataRequestAsync(uri, cancellationToken);
            if (IsLikelyWebPageResponse(response))
            {
                throw new InvalidOperationException(
                    "This link looks like a web page or confirmation page, not a direct downloadable file URL.");
            }

            var suggestedName = FileNameHelper.ExtractFileName(uri, response.Content.Headers.ContentDisposition);
            var fileName = FileNameHelper.GetUniqueFileName(saveDirectory, suggestedName);
            var finalPath = Path.Combine(saveDirectory, fileName);
            var tempPath = finalPath + ".part";
            var existingPartLength = File.Exists(tempPath) ? new FileInfo(tempPath).Length : 0;

            return new DownloadTaskInfo
            {
                Url = url,
                FileName = fileName,
                SaveDirectory = saveDirectory,
                FinalFilePath = finalPath,
                TempFilePath = tempPath,
                DownloadedBytes = existingPartLength,
                TotalBytes = response.Content.Headers.ContentLength,
                SupportsResume = SupportsResume(response.Headers),
                Status = "Queued",
                CreatedAt = DateTime.Now
            };
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("The server took too long to respond.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Unable to connect to the server: {ex.Message}");
        }
        finally
        {
            response?.Dispose();
        }
    }

    public async Task DownloadAsync(
        DownloadTaskInfo taskInfo,
        Action<DownloadTaskInfo>? onProgress,
        CancellationToken cancellationToken)
    {
        var existingLength = File.Exists(taskInfo.TempFilePath) ? new FileInfo(taskInfo.TempFilePath).Length : 0;
        var startingDownloadedBytes = existingLength;
        taskInfo.DownloadedBytes = existingLength;

        if (File.Exists(taskInfo.FinalFilePath) && existingLength == 0)
        {
            taskInfo.FinalFilePath = FileNameHelper.GetUniqueFilePath(taskInfo.FinalFilePath);
            taskInfo.FileName = Path.GetFileName(taskInfo.FinalFilePath);
            taskInfo.TempFilePath = taskInfo.FinalFilePath + ".part";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, taskInfo.Url);
        if (existingLength > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existingLength, null);
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (existingLength > 0 && response.StatusCode == HttpStatusCode.OK)
        {
            throw new ResumeNotSupportedException("The server does not support resuming this download.");
        }

        response.EnsureSuccessStatusCode();

        if (existingLength > 0 && response.StatusCode == HttpStatusCode.PartialContent)
        {
            taskInfo.SupportsResume = true;
            taskInfo.TotalBytes = response.Content.Headers.ContentRange?.Length
                                  ?? response.Content.Headers.ContentLength + existingLength;
        }
        else if (existingLength == 0)
        {
            taskInfo.TotalBytes = response.Content.Headers.ContentLength;
            taskInfo.SupportsResume = SupportsResume(response.Headers);
        }

        var fileMode = existingLength > 0 ? FileMode.Append : FileMode.Create;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(
            taskInfo.TempFilePath,
            fileMode,
            FileAccess.Write,
            FileShare.Read,
            81920,
            useAsync: true);

        var buffer = new byte[81920];
        var stopwatch = Stopwatch.StartNew();
        var lastSampleTime = stopwatch.Elapsed;
        var bytesSinceSample = 0L;
        var smoothedSpeed = 0d;
        const double smoothingFactor = 0.18;

        while (true)
        {
            var read = await contentStream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            taskInfo.DownloadedBytes += read;
            bytesSinceSample += read;

            var elapsedSinceSample = stopwatch.Elapsed - lastSampleTime;
            if (elapsedSinceSample.TotalMilliseconds >= 200)
            {
                var instantSpeed = bytesSinceSample / Math.Max(elapsedSinceSample.TotalSeconds, 0.001);
                smoothedSpeed = smoothedSpeed <= 0
                    ? instantSpeed
                    : (smoothedSpeed * (1 - smoothingFactor)) + (instantSpeed * smoothingFactor);

                var totalElapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
                var totalDownloadedThisSession = Math.Max(taskInfo.DownloadedBytes - startingDownloadedBytes, 0);
                var averageSessionSpeed = totalDownloadedThisSession / totalElapsedSeconds;
                var effectiveEtaSpeed = CalculateEffectiveEtaSpeed(smoothedSpeed, averageSessionSpeed);

                taskInfo.BytesPerSecond = smoothedSpeed;
                taskInfo.Eta = CalculateEta(taskInfo.TotalBytes, taskInfo.DownloadedBytes, effectiveEtaSpeed);
                onProgress?.Invoke(taskInfo);
                bytesSinceSample = 0;
                lastSampleTime = stopwatch.Elapsed;
            }
        }

        taskInfo.BytesPerSecond = 0;
        taskInfo.Eta = TimeSpan.Zero;
        onProgress?.Invoke(taskInfo);

        fileStream.Close();
        File.Move(taskInfo.TempFilePath, taskInfo.FinalFilePath, overwrite: true);
    }

    private async Task<HttpResponseMessage> SendMetadataRequestAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, uri);
        try
        {
            var headResponse = await _httpClient.SendAsync(
                headRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (headResponse.IsSuccessStatusCode)
            {
                return headResponse;
            }

            headResponse.Dispose();
        }
        catch
        {
        }

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, uri);
        var getResponse = await _httpClient.SendAsync(
            getRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        getResponse.EnsureSuccessStatusCode();
        return getResponse;
    }

    private static bool SupportsResume(HttpResponseHeaders headers)
    {
        return headers.AcceptRanges.Any(value => value.Equals("bytes", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsLikelyWebPageResponse(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return false;
        }

        return mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("application/xhtml+xml", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan? CalculateEta(long? totalBytes, long downloadedBytes, double bytesPerSecond)
    {
        if (!totalBytes.HasValue || totalBytes <= 0 || bytesPerSecond <= 0)
        {
            return null;
        }

        var remainingBytes = totalBytes.Value - downloadedBytes;
        if (remainingBytes <= 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(remainingBytes / bytesPerSecond);
    }

    private static double CalculateEffectiveEtaSpeed(double smoothedSpeed, double averageSessionSpeed)
    {
        if (smoothedSpeed <= 0)
        {
            return averageSessionSpeed;
        }

        if (averageSessionSpeed <= 0)
        {
            return smoothedSpeed;
        }

        return (smoothedSpeed * 0.7) + (averageSessionSpeed * 0.3);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

public sealed class ResumeNotSupportedException : Exception
{
    public ResumeNotSupportedException(string message) : base(message)
    {
    }
}
