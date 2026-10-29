namespace Nordlink.Contracts;

public record CloudLogin(string Password);
public record PairingCode(string Code, DateTimeOffset ExpiresAt);
public record HelperPairRequest(string Code);
public record HelperPairResponse(string Token, DateTimeOffset ExpiresAt);
// Route is a fixed relative API path, never a destination URL. Body never carries PIN material.
public record BridgeFrame(int Version, string RequestId, string Kind, string Route, string? Body, int Status, string ContentType);
