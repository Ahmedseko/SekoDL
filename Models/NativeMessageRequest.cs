namespace SekoDL.Models;

public sealed class NativeMessageRequest
{
    public string Action { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}
