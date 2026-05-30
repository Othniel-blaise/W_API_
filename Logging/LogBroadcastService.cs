using Microsoft.AspNetCore.SignalR;
using W_API.Api.Hubs;

namespace W_API.Api.Logging;

public class LogBroadcastService : BackgroundService
{
    private readonly IHubContext<LogHub> _hub;

    public LogBroadcastService(IHubContext<LogHub> hub) => _hub = hub;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var entry in LogChannel.Instance.Reader.ReadAllAsync(ct))
        {
            try
            {
                await _hub.Clients.All.SendAsync("ReceiveLog",
                    entry.Level, entry.Message, entry.Timestamp, ct);
            }
            catch
            {
                // ignore broadcast errors (client disconnected)
            }
        }
    }
}
