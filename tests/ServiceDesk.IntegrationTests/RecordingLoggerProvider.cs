using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ServiceDesk.IntegrationTests;

internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    internal ConcurrentQueue<string> Messages { get; } = new();
    public ILogger CreateLogger(string categoryName) => new Recorder(Messages);
    public void Dispose() { }
    private sealed class Recorder(ConcurrentQueue<string> messages) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
    }
}
