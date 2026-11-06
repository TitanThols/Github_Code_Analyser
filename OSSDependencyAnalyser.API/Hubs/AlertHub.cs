using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OSSDependencyAnalyzer.API.Hubs;

/// <summary>
/// Server → client push channel. Clients cannot invoke anything; messages are addressed with
/// <c>Clients.User(userId)</c>, which SignalR resolves from the NameIdentifier claim of the auth cookie.
/// </summary>
[Authorize]
public sealed class AlertHub : Hub
{
    public const string Path = "/hubs/alerts";
    public const string ReceiveAlert = "ReceiveAlert";
}
