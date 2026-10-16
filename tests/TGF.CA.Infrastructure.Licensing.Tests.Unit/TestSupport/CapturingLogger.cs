using Microsoft.Extensions.Logging;

namespace TGF.CA.Infrastructure.Licensing.Tests.Unit.TestSupport;

/// <summary>
/// Records every log entry as the formatted message, the exception and the raw structured state, so tests can prove a value is absent
/// from both what a console shows and what a structured sink (for example a JSON exporter) would receive.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T> {
    public List<CapturedLogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
        var structuredState = state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.Select(pair => $"{pair.Key}={pair.Value}").ToList()
            : [state?.ToString() ?? string.Empty];

        Entries.Add(new CapturedLogEntry(logLevel, formatter(state, exception), exception, structuredState));
    }

    /// <summary>Every observable text of every entry: formatted message, structured key/value pairs and exception text.</summary>
    public IEnumerable<string> AllObservableText()
        => Entries.SelectMany(entry => new[] { entry.Message }
            .Concat(entry.StructuredState)
            .Concat(entry.Exception is null ? [] : [entry.Exception.ToString()]));
}

internal sealed record CapturedLogEntry(LogLevel Level, string Message, Exception? Exception, IReadOnlyList<string> StructuredState);
