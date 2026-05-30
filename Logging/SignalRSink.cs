using Serilog.Core;
using Serilog.Events;

namespace W_API.Api.Logging;

public class SignalRSink : ILogEventSink
{
    // Patterns to suppress from the web terminal (keep PowerShell console clean,
    // not the live UI which is for ingestion events only)
    private static readonly string[] _suppressPatterns =
    [
        "HTTP \"GET\" \"/api/health\"",
        "HTTP \"GET\" \"/hubs/",
    ];

    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage();

        foreach (var pattern in _suppressPatterns)
            if (message.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return;

        var entry = new LogEntry(
            logEvent.Level.ToString(),
            message,
            DateTime.Now.ToString("HH:mm:ss")
        );
        LogChannel.Instance.Writer.TryWrite(entry);
    }
}
