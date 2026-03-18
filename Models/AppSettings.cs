namespace SekoDL.Models;

public sealed class AppSettings
{
    public string DefaultDownloadFolder { get; set; } = string.Empty;
    public int RefreshIntervalMs { get; set; } = 300;
    public bool EnableClipboardMonitor { get; set; } = false;
    public int ClipboardPollIntervalMs { get; set; } = 1000;
    public bool AutoImportExternalRequests { get; set; } = true;
    public string ExternalInboxFilePath { get; set; } = "Data/inbox.jsonl";
}
