using Serilog;

namespace VarthexComanda.Infrastructure.Logging;

public static class LoggingConfigurator
{
    // Teto de disco = QuantidadeMaximaArquivos x TamanhoMaximoArquivoBytes (padrao: 30 x 5 MB = 150 MB).
    // Com um arquivo por dia, 30 arquivos equivalem a ~30 dias em uso normal; o arquivo mais antigo e removido.
    public static ILogger CreateLogger(string logsDirectory, LogOptions? options = null, string? perfilUsuario = null)
    {
        options ??= new LogOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.TamanhoMaximoArquivoBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.QuantidadeMaximaArquivos, 1);

        var arquivo = new LoggerConfiguration()
            .WriteTo.File(
                path: Path.Combine(logsDirectory, "varthex-comanda-.log"),
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: options.TamanhoMaximoArquivoBytes,
                retainedFileCountLimit: options.QuantidadeMaximaArquivos,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // RF26: todo evento passa pela mascara de caminhos antes de chegar ao arquivo.
        return new LoggerConfiguration()
            .WriteTo.Sink(new MascaraCaminhosSink(arquivo, perfilUsuario))
            .CreateLogger();
    }
}
