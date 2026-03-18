namespace SekoDL.Services;

public sealed class ProtocolHandlerService
{
    private readonly UrlValidationService _urlValidationService;

    public ProtocolHandlerService(UrlValidationService urlValidationService)
    {
        _urlValidationService = urlValidationService;
    }

    public bool TryParseDownloadUrl(string argument, out string normalizedUrl, out string error)
    {
        normalizedUrl = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(argument))
        {
            error = "Protocol argument is empty.";
            return false;
        }

        if (!Uri.TryCreate(argument.Trim(), UriKind.Absolute, out var uri))
        {
            error = "Protocol argument is not a valid URI.";
            return false;
        }

        if (!string.Equals(uri.Scheme, "sekodl", StringComparison.OrdinalIgnoreCase))
        {
            error = "Unsupported protocol scheme.";
            return false;
        }

        if (!string.Equals(uri.Host, "download", StringComparison.OrdinalIgnoreCase))
        {
            error = "Unsupported SekoDL protocol action.";
            return false;
        }

        var encodedUrl = GetQueryParameter(uri.Query, "url");
        if (string.IsNullOrWhiteSpace(encodedUrl))
        {
            error = "The protocol link does not include a URL.";
            return false;
        }

        var decodedUrl = Uri.UnescapeDataString(encodedUrl.Replace('+', ' '));
        return _urlValidationService.TryNormalizeSupportedHttpUrl(decodedUrl, out normalizedUrl, out error);
    }

    private static string? GetQueryParameter(string query, string name)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 0)
            {
                continue;
            }

            if (string.Equals(parts[0], name, StringComparison.OrdinalIgnoreCase))
            {
                return parts.Length > 1 ? parts[1] : string.Empty;
            }
        }

        return null;
    }
}
