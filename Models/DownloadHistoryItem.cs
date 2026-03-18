namespace SekoDL.Models;

public sealed class DownloadHistoryItem
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FinalFilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long DownloadedBytes { get; set; }
    public long? TotalBytes { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
