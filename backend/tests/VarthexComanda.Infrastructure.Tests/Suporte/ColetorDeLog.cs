using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace VarthexComanda.Infrastructure.Tests.Suporte;

/// <summary>Sink de teste que guarda os eventos de log emitidos por um <see cref="ILogger"/> real.</summary>
public sealed class ColetorDeLog : ILogEventSink
{
    public ColetorDeLog()
    {
        Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(this).CreateLogger();
    }

    public ILogger Logger { get; }

    public List<LogEvent> Eventos { get; } = new();

    public void Emit(LogEvent logEvent) => Eventos.Add(logEvent);
}
