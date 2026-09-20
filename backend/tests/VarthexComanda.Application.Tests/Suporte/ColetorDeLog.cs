using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace VarthexComanda.Application.Tests.Suporte;

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

    /// <summary>Mensagem renderizada, propriedades e excecao de todos os eventos, como o arquivo de log veria.</summary>
    public string TextoCompleto => string.Join(
        "\n",
        Eventos.Select(e => e.RenderMessage() + " "
            + string.Join(" ", e.Properties.Select(p => p.Key + "=" + p.Value)) + " "
            + e.Exception));

    /// <summary>Um unico evento, no nivel esperado, com a excecao e a propriedade Operacao, sem dados de negocio.</summary>
    public void AfirmarFalhaTecnica(LogEventLevel nivel, string operacao, params string[] dadosDeNegocio)
    {
        var evento = Assert.Single(Eventos);
        Assert.Equal(nivel, evento.Level);
        Assert.NotNull(evento.Exception);
        var propriedade = Assert.IsType<ScalarValue>(evento.Properties["Operacao"]);
        Assert.Equal(operacao, propriedade.Value);

        var texto = TextoCompleto;
        foreach (var dado in dadosDeNegocio)
        {
            Assert.DoesNotContain(dado, texto, StringComparison.OrdinalIgnoreCase);
        }
    }
}
