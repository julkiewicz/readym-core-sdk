using Microsoft.Extensions.Logging;
using Xunit.Sdk;

namespace ReadyM.Api.Tests;

/// Throws on any log at Error or above, so an unexpected error fails the test.
public sealed class FailOnErrorLoggerProvider : ILoggerProvider
{
    private sealed class Logger(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
                throw new XunitException($"[{logLevel}] in {categoryName}: {formatter(state, exception)}", exception);
        }
    }

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName);

    public void Dispose()
    {
        // No resources to dispose
    }
}
