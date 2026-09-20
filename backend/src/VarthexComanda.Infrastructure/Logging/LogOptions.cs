namespace VarthexComanda.Infrastructure.Logging;

public sealed record LogOptions(
    long TamanhoMaximoArquivoBytes = 5 * 1024 * 1024,
    int QuantidadeMaximaArquivos = 30);
