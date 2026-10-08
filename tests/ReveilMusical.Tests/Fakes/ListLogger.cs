using Microsoft.Extensions.Logging;

namespace ReveilMusical.Tests.Fakes;

/// <summary>Logger qui garde les entrées en mémoire pour les assertions.</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add((logLevel, formatter(state, exception)));
    }

    public int Count(LogLevel level) => Entries.Count(e => e.Level == level);
}
