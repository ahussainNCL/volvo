using System.Net.WebSockets;
using System.Text.Json;

namespace Nordlink.Contracts;

public static class WebSocketFrames
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public static async Task Send(WebSocket socket, BridgeFrame frame, SemaphoreSlim gate, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame, WireJson.Options);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Bridge message too large.");
        await gate.WaitAsync(ct);
        try { await socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct); }
        finally { gate.Release(); }
    }
    public static async Task<BridgeFrame?> Read(WebSocket socket, CancellationToken ct)
    {
        using var buffer = new MemoryStream(); var block = new byte[8192];
        WebSocketReceiveResult received;
        do
        {
            received = await socket.ReceiveAsync(block, ct);
            if (received.MessageType == WebSocketMessageType.Close) return null;
            if (received.MessageType != WebSocketMessageType.Text || buffer.Length + received.Count > MaxBytes)
                throw new InvalidDataException("Invalid bridge message.");
            buffer.Write(block, 0, received.Count);
        } while (!received.EndOfMessage);
        var frame = JsonSerializer.Deserialize<BridgeFrame>(buffer.ToArray(), WireJson.Options) ?? throw new InvalidDataException("Missing bridge message.");
        if (frame.Version != 1 || !Guid.TryParseExact(frame.RequestId, "N", out _)) throw new InvalidDataException("Invalid bridge version/request ID.");
        return frame;
    }
    public static bool Allowed(string method, string route)
    {
        if (route.Contains('?') || route.Contains('#') || route.Contains('%') || route.Contains("..")) return false;
        if (method == "GET" && new[] { "/api/status", "/api/vehicles", "/api/operations", "/api/recordings", "/api/artifacts" }.Contains(route)) return true;
        if (method == "POST" && new[] { "/api/environment", "/api/drivers/inspect", "/api/connect", "/api/disconnect", "/api/operations/review", "/api/recordings/simulation-fixture", "/api/live/simulation/start", "/api/live/simulation/stop" }.Contains(route)) return true;
        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || parts[0] != "api") return false;
        if (method == "GET" && parts[1] == "recordings" && Guid.TryParseExact(parts[2], "N", out _) &&
            (parts.Length == 3 || parts.Length == 4 && parts[3] == "csv")) return true;
        return method == "POST" && parts.Length == 4 && parts[1] == "operations" &&
            Guid.TryParseExact(parts[2], "N", out _) && parts[3] == "execute";
    }
}
