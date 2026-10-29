using Microsoft.AspNetCore.SignalR;

namespace Nordlink.Gateway;

// Authentication and strict Origin validation run before the SignalR endpoint.
public sealed class ProgressHub : Hub;
