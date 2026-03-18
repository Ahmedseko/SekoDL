namespace SekoDL.Models;

public sealed class DownloadTaskInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string SaveDirectory { get; set; } = string.Empty;
    public string TempFilePath { get; set; } = string.Empty;
    public string FinalFilePath { get; set; } = string.Empty;
    public long DownloadedBytes { get; set; }
    public long? TotalBytes { get; set; }
    public bool SupportsResume { get; set; }
    public string Status { get; set; } = "Queued";
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public double BytesPerSecond { get; set; }
    public TimeSpan? Eta { get; set; }

    public string SupportsResumeMessage => SupportsResume ? "Resume supported" : "Resume unsupported";
}
