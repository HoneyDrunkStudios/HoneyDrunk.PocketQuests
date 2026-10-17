using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace PocketQuests.Tests.Api;

internal sealed class ApiTestLog : ILoggerProvider
{
    internal ConcurrentQueue<(LogLevel level, string category, string message, Exception? error)> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Sink(categoryName, Entries);

    public void Dispose()
    {
    }

    private sealed class Sink(string category, ConcurrentQueue<(LogLevel level, string category, string message, Exception? error)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue((logLevel, category, formatter(state, exception), exception));
    }
}
