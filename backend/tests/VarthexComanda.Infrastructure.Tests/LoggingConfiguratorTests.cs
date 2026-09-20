using VarthexComanda.Infrastructure.Logging;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class LoggingConfiguratorTests
{
    [Fact]
    public void CreateLogger_EscreveArquivoDeLogNaPastaIndicada()
    {
        var logsDir = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(logsDir);
        try
        {
            var logger = LoggingConfigurator.CreateLogger(logsDir);

            logger.Information("mensagem de teste {Marcador}", "abc123");
            (logger as IDisposable)?.Dispose();

            var arquivos = Directory.GetFiles(logsDir, "varthex-comanda-*.log");
            Assert.Single(arquivos);
            Assert.Contains("abc123", File.ReadAllText(arquivos[0]));
        }
        finally
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }

    // CT22, RNF21
    [Fact]
    public void CreateLogger_RolaPorTamanho()
    {
        var logsDir = NovaPasta();
        try
        {
            var logger = LoggingConfigurator.CreateLogger(logsDir, new LogOptions(TamanhoMaximoArquivoBytes: 2048, QuantidadeMaximaArquivos: 30));

            for (var i = 0; i < 200; i++)
                logger.Information("{Linha}", new string('x', 100));
            (logger as IDisposable)?.Dispose();

            var arquivos = Directory.GetFiles(logsDir, "varthex-comanda-*.log");
            Assert.True(arquivos.Length >= 2, $"esperava >= 2 arquivos, havia {arquivos.Length}");
            foreach (var arquivo in arquivos)
                Assert.True(new FileInfo(arquivo).Length <= 2048 + 512, $"{arquivo} passou do limite");
        }
        finally
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }

    // CT22, RNF21
    [Fact]
    public void CreateLogger_RetemNoMaximoNArquivos()
    {
        var logsDir = NovaPasta();
        try
        {
            var logger = LoggingConfigurator.CreateLogger(logsDir, new LogOptions(1024, 3));

            for (var i = 0; i < 100; i++)
                logger.Information("{Linha}", new string('x', 100));
            logger.Information("ULTIMO-MARCADOR-{Id}", "z9z9");
            (logger as IDisposable)?.Dispose();

            var arquivos = Directory.GetFiles(logsDir, "varthex-comanda-*.log");
            Assert.Equal(3, arquivos.Length);
            Assert.Contains(arquivos, a => File.ReadAllText(a).Contains("ULTIMO-MARCADOR-z9z9"));
        }
        finally
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }

    [Theory]
    [InlineData(0L, 3)]
    [InlineData(-1L, 3)]
    [InlineData(1024L, 0)]
    public void CreateLogger_OpcoesInvalidas_Lancam(long tamanho, int quantidade)
    {
        var logsDir = NovaPasta();
        try
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                LoggingConfigurator.CreateLogger(logsDir, new LogOptions(tamanho, quantidade)));
        }
        finally
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }

    private static string NovaPasta()
    {
        var logsDir = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(logsDir);
        return logsDir;
    }
}
