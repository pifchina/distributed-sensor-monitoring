using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Hubs;

// Clients only subscribe to broadcasts; the server pushes alarms via IHubContext,
// so no client-to-server methods are required here.
public sealed class AlarmsHub : Hub
{
}
