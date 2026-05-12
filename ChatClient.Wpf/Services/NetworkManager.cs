using System.Net.Sockets;
using System.Text.Json;
using ChatClient.Wpf.Models;

namespace ChatClient.Wpf.Services;

public sealed class NetworkManager
{
    private const int MaxMessageBytes = 32 * 1024 * 1024;

    public async Task SendMessage(NetworkStream stream, NetworkMessage message, CancellationToken cancellationToken = default)
    {
        byte[] data = SerializeMessage(message);
        if (data.Length > MaxMessageBytes)
        {
            throw new InvalidOperationException($"Message payload exceeds the {MaxMessageBytes / (1024 * 1024)} MB limit.");
        }

        byte[] lengthPrefix = BitConverter.GetBytes(data.Length);
        await stream.WriteAsync(lengthPrefix, cancellationToken);
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public async Task<NetworkMessage?> ReceiveMessage(NetworkStream stream, CancellationToken cancellationToken = default)
    {
        byte[]? lengthBuffer = await ReadExact(stream, sizeof(int), cancellationToken);
        if (lengthBuffer == null)
        {
            return null;
        }

        int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
        if (messageLength <= 0)
        {
            return null;
        }

        if (messageLength > MaxMessageBytes)
        {
            throw new InvalidOperationException($"Incoming message exceeds the {MaxMessageBytes / (1024 * 1024)} MB limit.");
        }

        byte[]? payload = await ReadExact(stream, messageLength, cancellationToken);
        if (payload == null)
        {
            return null;
        }

        return DeserializeMessage(payload);
    }

    public byte[] SerializeMessage(NetworkMessage message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message);
    }

    public NetworkMessage DeserializeMessage(byte[] data)
    {
        NetworkMessage? message = JsonSerializer.Deserialize<NetworkMessage>(data);
        if (message == null)
        {
            throw new InvalidOperationException("Failed to deserialize incoming network message.");
        }

        return message;
    }

    private static async Task<byte[]?> ReadExact(NetworkStream stream, int byteCount, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[byteCount];
        int offset = 0;
        while (offset < byteCount)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, byteCount - offset), cancellationToken);
            if (read == 0)
            {
                return null;
            }

            offset += read;
        }

        return buffer;
    }
}
