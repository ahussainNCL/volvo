using System.Net.WebSockets;
using System.Text.Json;
using Nordlink.Cloud;
using Nordlink.Contracts;
using Xunit;

namespace Nordlink.Tests;

public sealed class CloudTests
{
    private sealed class Socket : WebSocket
    {
        private WebSocketState state = WebSocketState.Open;
        public byte[] LastMessage = [];
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => state;
        public override string? SubProtocol => null;
        public override void Abort() => state = WebSocketState.Aborted;
        public override void Dispose() => state = WebSocketState.Closed;
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) { state = WebSocketState.Closed; return Task.CompletedTask; }
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => CloseAsync(closeStatus, statusDescription, cancellationToken);
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) { LastMessage = buffer.ToArray(); return Task.CompletedTask; }
    }
    [Fact] public void PairingCodeIsOneUseAndBoundToBrowserSession()
    {
        var relay = new Relay(); var browser = relay.CreateSession(); var other = relay.CreateSession();
        var code = relay.Pair(browser); var paired = relay.Exchange(code.Code)!;
        Assert.NotNull(paired); Assert.Null(relay.Exchange(code.Code)); Assert.Equal(browser, relay.AuthenticateHelper(paired.Token));
        Assert.False(relay.Connected(other)); relay.Remove(browser); Assert.Null(relay.AuthenticateHelper(paired.Token));
    }
    [Fact] public void NewPairingInvalidatesPreviousCodeAndHelperCredential()
    {
        var relay = new Relay(); var id = relay.CreateSession(); var old = relay.Exchange(relay.Pair(id).Code)!;
        var newer = relay.Pair(id); Assert.Null(relay.AuthenticateHelper(old.Token));
        Assert.False(relay.Attach(id, new Socket(), old.Token));
        var current = relay.Exchange(newer.Code)!; Assert.True(relay.Attach(id, new Socket(), current.Token));
        Assert.False(relay.Attach(id, new Socket(), current.Token));
    }
    [Fact] public void OldSocketCleanupCannotDisconnectNewlyPairedHelper()
    {
        var relay = new Relay(); var id = relay.CreateSession(); var oldSocket = new Socket();
        Assert.True(relay.Attach(id, oldSocket, relay.Exchange(relay.Pair(id).Code)!.Token));
        relay.Detach(id); var current = new Socket(); Assert.True(relay.Attach(id, current, relay.Exchange(relay.Pair(id).Code)!.Token));
        relay.Detach(id, oldSocket); Assert.True(relay.Connected(id));
    }
    [Fact] public async Task ResponsesCannotResolveAnotherBrowserRequest()
    {
        var relay = new Relay(); var id = relay.CreateSession(); var other = relay.CreateSession(); var socket = new Socket();
        relay.Attach(id, socket, relay.Exchange(relay.Pair(id).Code)!.Token);
        var request = relay.Request(id, "GET", "/api/status", null, CancellationToken.None);
        var sent = JsonSerializer.Deserialize<BridgeFrame>(socket.LastMessage, WireJson.Options)!;
        var response = sent with { Kind = "response", Status = 200, Body = "{}" };
        relay.Complete(other, response); Assert.False(request.IsCompleted);
        relay.Complete(id, response); Assert.Equal(response, await request);
    }
    [Fact] public async Task HelperDisconnectFailsPendingRequestsWithoutRetry()
    {
        var relay = new Relay(); var id = relay.CreateSession(); var socket = new Socket();
        relay.Attach(id, socket, relay.Exchange(relay.Pair(id).Code)!.Token);
        var request = relay.Request(id, "GET", "/api/status", null, CancellationToken.None);
        relay.Detach(id); await Assert.ThrowsAsync<IOException>(() => request); Assert.False(relay.Connected(id));
    }
    [Fact] public async Task WrongResponseRouteIsRejected()
    {
        var relay = new Relay(); var id = relay.CreateSession(); var socket = new Socket();
        relay.Attach(id, socket, relay.Exchange(relay.Pair(id).Code)!.Token);
        var request = relay.Request(id, "GET", "/api/status", null, CancellationToken.None);
        var sent = JsonSerializer.Deserialize<BridgeFrame>(socket.LastMessage, WireJson.Options)!;
        relay.Complete(id, sent with { Kind = "response", Route = "/api/credentials", Status = 200 });
        await Assert.ThrowsAsync<InvalidDataException>(() => request);
    }
    [Theory]
    [InlineData("GET", "/api/credentials")]
    [InlineData("POST", "/api/credentials/abc/reveal")]
    [InlineData("POST", "/api/artifacts")]
    [InlineData("POST", "/api/session")]
    [InlineData("GET", "https://other.invalid/api/status")]
    [InlineData("GET", "/api/status?url=other")]
    [InlineData("GET", "/api/../credentials")]
    [InlineData("POST", "/api/capabilities/firmware/run")]
    public void CloudAndHelperShareExplicitRouteRestrictions(string method, string route) => Assert.False(WebSocketFrames.Allowed(method, route));
    [Theory] [InlineData("GET", "/api/status")] [InlineData("POST", "/api/disconnect")]
    public void SupportedRoutesAreAllowed(string method, string route) => Assert.True(WebSocketFrames.Allowed(method, route));
}
