using System.Net.Http.Headers;

namespace SekoDL.Utils;

public static class FileNameHelper
{
    public static string ExtractFileName(Uri uri, ContentDispositionHeaderValue? contentDisposition)
    {
        var fileName = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        fileName = fileName?.Trim('"');

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = Path.GetFileName(uri.LocalPath);
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "download.bin";
        }

        return SanitizeFileName(fileName);
    }

    public static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleaned = new string(fileName
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(cleaned) ? "download.bin" : cleaned;
    }

    public static string GetUniqueFileName(string directory, string fileName)
    {
        var candidatePath = Path.Combine(directory, fileName);
        return Path.GetFileName(GetUniqueFilePath(candidatePath));
    }

    public static string GetUniqueFilePath(string path)
    {
        if (!File.Exists(path) && !File.Exists(path + ".part"))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path) ?? Directory.GetCurrentDirectory();
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var index = 1;

        while (true)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate) && !File.Exists(candidate + ".part"))
            {
                return candidate;
            }

            index++;
        }
    }
}
