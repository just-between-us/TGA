using Microsoft.Extensions.Logging;
using TGA.Contract.DTOs;

namespace TGA.Infrastructure.Diagnostics;

public class InMemoryLoggerProvider(InMemoryLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, sink);
    public void Dispose() { }

    private class InMemoryLogger(string categoryName, InMemoryLogSink sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            sink.Add(new LogEntryDto(
                DateTime.Now, logLevel.ToString(), categoryName,
                formatter(state, exception), exception?.Message));
        }
    }
}