using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Nordlink.Contracts;

namespace Nordlink.Cloud;

public sealed class Relay
{
    private sealed class Session
    {
        public DateTimeOffset ExpiresAt = DateTimeOffset.UtcNow.AddHours(8);
        public string? PairHash, HelperHash;
        public DateTimeOffset PairExpires;
        public WebSocket? Socket;
        public readonly SemaphoreSlim SendGate = new(1, 1);
        public readonly ConcurrentDictionary<string, TaskCompletionSource<BridgeFrame>> Pending = new();
    }
    private readonly ConcurrentDictionary<string, Session> sessions = new();
    public string CreateSession()
    {
        foreach (var pair in sessions.Where(p => p.Value.ExpiresAt <= DateTimeOffset.UtcNow)) Remove(pair.Key);
        if (sessions.Count >= 20) throw new InvalidOperationException("Too many active sessions. Sign out an existing browser or wait for expiry.");
        var id = Guid.NewGuid().ToString("N"); sessions[id] = new(); return id;
    }
    public bool Active(string id) => sessions.TryGetValue(id, out var s) && s.ExpiresAt > DateTimeOffset.UtcNow;
    public bool Connected(string id) => sessions.TryGetValue(id, out var s) && s.Socket?.State == WebSocketState.Open;
    public PairingCode Pair(string id)
    {
        if (!Active(id)) throw new InvalidOperationException("Session expired.");
        var s = sessions[id]; lock (s)
        {
            if (s.Socket is not null) throw new InvalidOperationException("Unpair the existing helper first.");
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
            s.PairHash = Hash(code); s.PairExpires = DateTimeOffset.UtcNow.AddMinutes(5);
            s.HelperHash = null;
            return new(code, s.PairExpires);
        }
    }
    public HelperPairResponse? Exchange(string code)
    {
        if (code.Length != 16) return null;
        foreach (var s in sessions.Values)
        {
            lock (s)
            {
                if (s.PairExpires <= DateTimeOffset.UtcNow || s.ExpiresAt <= DateTimeOffset.UtcNow || s.PairHash != Hash(code)) continue;
                s.PairHash = null; var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                s.HelperHash = Hash(token); return new(token, s.ExpiresAt);
            }
        }
        return null;
    }
    public string? AuthenticateHelper(string token)
    {
        if (token.Length != 64) return null;
        var hash = Hash(token);
        return sessions.FirstOrDefault(p => p.Value.ExpiresAt > DateTimeOffset.UtcNow && p.Value.HelperHash == hash).Key;
    }
    public bool Attach(string id, WebSocket socket, string token)
    {
        if (!sessions.TryGetValue(id, out var s) || !Active(id)) return false;
        lock (s) { if (s.Socket is not null || s.HelperHash != Hash(token) || !Active(id)) return false; s.Socket = socket; return true; }
    }
    public void Detach(string id, WebSocket? expectedSocket = null)
    {
        if (!sessions.TryGetValue(id, out var s)) return;
        lock (s)
        {
            if (expectedSocket is not null && !ReferenceEquals(expectedSocket, s.Socket)) return;
            s.Socket?.Abort(); s.Socket = null; s.HelperHash = null; s.PairHash = null;
        }
        foreach (var pending in s.Pending.Values) pending.TrySetException(new IOException("Windows helper disconnected; results may be unknown. Re-pair explicitly; commands are never retried."));
    }
    public void Remove(string id)
    { Detach(id); sessions.TryRemove(id, out _); }
    public void Complete(string id, BridgeFrame frame)
    { if (sessions.TryGetValue(id, out var s) && s.Pending.TryGetValue(frame.RequestId, out var pending)) pending.TrySetResult(frame); }
    public async Task<BridgeFrame> Request(string id, string method, string route, string? body, CancellationToken ct)
    {
        if (!Active(id) || !sessions.TryGetValue(id, out var s) || s.Socket?.State != WebSocketState.Open)
            throw new IOException("Pair and connect the Windows helper first.");
        if (!WebSocketFrames.Allowed(method, route)) throw new InvalidOperationException("This route is unavailable through the cloud. Credentials and artifact uploads stay local.");
        if (s.Pending.Count >= 16) throw new InvalidOperationException("Too many pending helper requests.");
        var requestId = Guid.NewGuid().ToString("N"); var pending = new TaskCompletionSource<BridgeFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        s.Pending[requestId] = pending;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            await WebSocketFrames.Send(s.Socket, new(1, requestId, method, route, body, 0, "application/json"), s.SendGate, deadline.Token);
            var response = await pending.Task.WaitAsync(deadline.Token);
            if (response.Route != route) throw new InvalidDataException("Helper response route mismatch.");
            return response;
        }
        finally { s.Pending.TryRemove(requestId, out _); }
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
