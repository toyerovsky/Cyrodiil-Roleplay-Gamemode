using Microsoft.Extensions.Logging;

namespace CyrodiilRP.Tests.Fakes;

internal sealed class ListLogger : ILogger
{
    private readonly List<LoggedLine> _lines = [];

    public IReadOnlyList<LoggedLine> Lines
    {
        get
        {
            lock (_lines)
                return [.. _lines];
        }
    }

    public IEnumerable<LoggedLine> Errors => Lines.Where(line => line.Level == LogLevel.Error);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (_lines)
            _lines.Add(new LoggedLine(logLevel, formatter(state, exception), exception));
    }
}
