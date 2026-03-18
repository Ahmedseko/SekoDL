namespace SekoDL.Models;

public sealed class SpeedTestResult
{
    public double AverageLatencyMs { get; set; }
    public double JitterMs { get; set; }
    public double DownloadMbps { get; set; }
    public double DownloadMegabytesPerSecond { get; set; }
    public long BytesDownloaded { get; set; }
    public TimeSpan TestDuration { get; set; }
    public string TestServer { get; set; } = string.Empty;
}
