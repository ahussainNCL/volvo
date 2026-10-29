using System.Buffers.Binary;
using System.Text.Json;
using Nordlink.Contracts;

namespace Nordlink.Contracts;

public static class PipeProtocol
{
    public const int Version = 1;
    public const int MaxPayload = 1024 * 1024;
    public static async Task Write<T>(Stream stream, T message, CancellationToken ct)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, WireJson.Options);
        if (payload.Length is < 1 or > MaxPayload) throw new InvalidDataException("IPC payload size invalid.");
        var prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, ct); await stream.WriteAsync(payload, ct); await stream.FlushAsync(ct);
    }
    public static async Task<T> Read<T>(Stream stream, CancellationToken ct)
    {
        var prefix = new byte[4]; await stream.ReadExactlyAsync(prefix, ct);
        var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length is < 1 or > MaxPayload) throw new InvalidDataException("IPC payload size invalid.");
        var payload = new byte[length]; await stream.ReadExactlyAsync(payload, ct);
        return JsonSerializer.Deserialize<T>(payload, WireJson.Options) ?? throw new InvalidDataException("IPC message is null.");
    }
}
