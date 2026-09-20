using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;
using Serilog.Parsing;

namespace VarthexComanda.Infrastructure.Logging;

// Sink decorador: renderiza o evento, mascara o perfil do usuario (mensagem e excecao)
// e repassa um evento novo ao sink real. RF26.
public sealed class MascaraCaminhosSink : ILogEventSink, IDisposable
{
    private static readonly MessageTemplate Template = new MessageTemplateParser().Parse("{Mensagem}");

    private static readonly MessageTemplateTextFormatter FormatadorMensagem = new("{Message:lj}");

    private readonly ILogEventSink _interno;
    private readonly Func<string, string> _mascara;

    public MascaraCaminhosSink(ILogEventSink interno, string? perfilUsuario = null, PastasMascaradas? pastas = null)
    {
        _interno = interno;
        _mascara = LogMascara.CriarMascara(perfilUsuario, pastas);
    }

    public void Emit(LogEvent logEvent)
    {
        var mensagem = _mascara(Renderizar(logEvent));
        var excecao = logEvent.Exception is null
            ? null
            : new ExcecaoMascarada(_mascara(logEvent.Exception.ToString()));

        var propriedades = new[] { new LogEventProperty("Mensagem", new ScalarValue(mensagem)) };
        _interno.Emit(new LogEvent(logEvent.Timestamp, logEvent.Level, excecao, Template, propriedades));
    }

    // Mesmo formato do {Message:lj} do output template: strings sem aspas nem escape de barras.
    private static string Renderizar(LogEvent logEvent)
    {
        using var writer = new StringWriter();
        FormatadorMensagem.Format(logEvent, writer);
        return writer.ToString();
    }

    public void Dispose() => (_interno as IDisposable)?.Dispose();

    // Carrega o texto ja mascarado (tipo, mensagem e stack) para o {Exception} do output template.
    private sealed class ExcecaoMascarada : Exception
    {
        private readonly string _texto;

        public ExcecaoMascarada(string texto) : base(texto) => _texto = texto;

        public override string ToString() => _texto;
    }
}
