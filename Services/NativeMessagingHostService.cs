using System.Text;
using System.Text.Json;
using SekoDL.Models;

namespace SekoDL.Services;

public sealed class NativeMessagingHostService
{
    private readonly UrlValidationService _urlValidationService;
    private readonly ExternalInboxService _externalInboxService;

    public NativeMessagingHostService(
        UrlValidationService urlValidationService,
        ExternalInboxService externalInboxService)
    {
        _urlValidationService = urlValidationService;
        _externalInboxService = externalInboxService;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var input = Console.OpenStandardInput();
        await using var output = Console.OpenStandardOutput();

        while (!cancellationToken.IsCancellationRequested)
        {
            var request = await ReadMessageAsync(input, cancellationToken);
            if (request is null)
            {
                break;
            }

            var response = await HandleRequestAsync(request);
            await WriteMessageAsync(output, response, cancellationToken);
        }
    }

    private async Task<NativeMessageResponse> HandleRequestAsync(NativeMessageRequest request)
    {
        if (!string.Equals(request.Action, "enqueue", StringComparison.OrdinalIgnoreCase))
        {
            return new NativeMessageResponse
            {
                Ok = false,
                Message = "Unsupported action. Only 'enqueue' is allowed."
            };
        }

        if (!_urlValidationService.TryNormalizeSupportedHttpUrl(request.Url, out var normalizedUrl, out var error))
        {
            return new NativeMessageResponse
            {
                Ok = false,
                Message = error
            };
        }

        await _externalInboxService.AppendRequestAsync(normalizedUrl);
        return new NativeMessageResponse
        {
            Ok = true,
            Message = "Request accepted and written to inbox."
        };
    }

    private static async Task<NativeMessageRequest?> ReadMessageAsync(Stream input, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        var bytesRead = await ReadExactAsync(input, lengthBuffer, cancellationToken);
        if (bytesRead == 0)
        {
            return null;
        }

        if (bytesRead < 4)
        {
            return new NativeMessageRequest();
        }

        var messageLength = BitConverter.ToInt32(lengthBuffer, 0);
        if (messageLength <= 0 || messageLength > 1024 * 1024)
        {
            return new NativeMessageRequest();
        }

        var payloadBuffer = new byte[messageLength];
        var payloadRead = await ReadExactAsync(input, payloadBuffer, cancellationToken);
        if (payloadRead < messageLength)
        {
            return new NativeMessageRequest();
        }

        try
        {
            var json = Encoding.UTF8.GetString(payloadBuffer);
            return JsonSerializer.Deserialize<NativeMessageRequest>(json) ?? new NativeMessageRequest();
        }
        catch
        {
            return new NativeMessageRequest();
        }
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0)
            {
                return total;
            }

            total += read;
        }

        return total;
    }

    private static async Task WriteMessageAsync(Stream output, NativeMessageResponse response, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response);
        var payload = Encoding.UTF8.GetBytes(json);
        var length = BitConverter.GetBytes(payload.Length);

        await output.WriteAsync(length, cancellationToken);
        await output.WriteAsync(payload, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }
}
