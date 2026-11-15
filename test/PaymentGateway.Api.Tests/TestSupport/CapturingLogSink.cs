using System.Collections.Concurrent;

using Serilog.Core;
using Serilog.Events;

namespace PaymentGateway.Api.Tests.TestSupport;

/// <summary>
/// Records every Serilog event the application writes. The application picks it up through
/// <c>ReadFrom.Services</c>, so tests see exactly what production would write. A hand-written sink because
/// Serilog's <c>ILogEventSink</c> is the natural capture point and a mock would only wrap a list.
/// </summary>
public sealed class CapturingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => [.. _events];

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);

    /// <summary>Everything an event would show a reader: rendered message, properties and exception.</summary>
    public IEnumerable<string> RenderedEvents() =>
        Events.Select(e => $"{e.RenderMessage()} {string.Join(' ', e.Properties.Select(p => p.Value.ToString()))} {e.Exception}");
}