using System.Threading.Channels;

namespace W_API.Api.Logging;

public record LogEntry(string Level, string Message, string Timestamp);

public static class LogChannel
{
    public static readonly Channel<LogEntry> Instance =
        Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
}
