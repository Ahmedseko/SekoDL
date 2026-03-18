namespace SekoDL.Services;

public sealed class UrlValidationService
{
    private static readonly HashSet<string> UnsupportedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "file",
        "javascript",
        "data",
        "ftp",
        "blob",
        "chrome",
        "edge"
    };

    public bool TryNormalizeSupportedHttpUrl(string input, out string normalizedUrl, out string error)
    {
        normalizedUrl = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "URL is empty.";
            return false;
        }

        var trimmed = input.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            error = "URL is not a valid absolute address.";
            return false;
        }

        if (UnsupportedSchemes.Contains(uri.Scheme))
        {
            error = $"The '{uri.Scheme}' scheme is not supported.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "Only direct HTTP or HTTPS links are supported.";
            return false;
        }

        normalizedUrl = uri.AbsoluteUri;
        return true;
    }
}
