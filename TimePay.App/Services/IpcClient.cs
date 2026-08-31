using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using TimePay.Core.Ipc;

namespace TimePay.App.Services;

/// <summary>
/// IPC Client used by the WPF application to communicate with the background TimePay Windows Service.
/// </summary>
public class IpcClient
{
    public async Task<bool> IsServiceAvailableAsync(int timeoutMs = 500)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", IpcConstants.PipeName, PipeDirection.InOut);
            await client.ConnectAsync(timeoutMs);
            return client.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IpcMessage?> SendRequestAsync(IpcMessage request, int timeoutMs = 2000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", IpcConstants.PipeName, PipeDirection.InOut);
            await client.ConnectAsync(timeoutMs);

            using var reader = new StreamReader(client);
            using var writer = new StreamWriter(client) { AutoFlush = true };

            var requestJson = JsonSerializer.Serialize(request);
            await writer.WriteLineAsync(requestJson);

            var responseJson = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(responseJson))
                return null;

            return JsonSerializer.Deserialize<IpcMessage>(responseJson);
        }
        catch
        {
            return null;
        }
    }

    public async Task<ServiceStatusDto?> GetServiceStatusAsync(int timeoutMs = 1500)
    {
        var response = await SendRequestAsync(new IpcMessage { Type = IpcMessageType.GetStatusRequest }, timeoutMs);
        if (response != null && response.Success && !string.IsNullOrEmpty(response.Payload))
        {
            return JsonSerializer.Deserialize<ServiceStatusDto>(response.Payload);
        }
        return null;
    }
}
