using VarthexComanda.Infrastructure.Logging;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class LogMascaraTests
{
    // RF26, docs/08
    [Fact]
    public void Aplicar_TrocaPerfilPorVariavel()
    {
        var r = LogMascara.Aplicar(@"C:\Users\maria\AppData\Local\X", @"C:\Users\maria");
        Assert.Equal(@"%USERPROFILE%\AppData\Local\X", r);
    }

    [Fact]
    public void Aplicar_AceitaBarraNormalEMaiusculasDiferentes()
    {
        var r = LogMascara.Aplicar("c:/users/MARIA/Fotos/a.jpg", @"C:\Users\maria");
        Assert.Equal("%USERPROFILE%/Fotos/a.jpg", r);
    }

    [Fact]
    public void Aplicar_NaoAlteraTextoSemOPerfil()
    {
        const string texto = @"Falha em D:\dados\banco.db";
        Assert.Equal(texto, LogMascara.Aplicar(texto, @"C:\Users\maria"));
    }

    [Fact]
    public void Aplicar_NaoCasaUsuarioComNomeMaior()
    {
        const string texto = @"C:\Users\maria2\x";
        Assert.Equal(texto, LogMascara.Aplicar(texto, @"C:\Users\maria"));
    }

    [Fact]
    public void Aplicar_TrocaVariasOcorrencias()
    {
        var r = LogMascara.Aplicar(@"de C:\Users\maria\a para C:\Users\maria\b", @"C:\Users\maria");
        Assert.Equal(@"de %USERPROFILE%\a para %USERPROFILE%\b", r);
    }

    [Fact]
    public void Aplicar_PerfilVazioOuTextoVazio_NaoAltera()
    {
        Assert.Equal("abc", LogMascara.Aplicar("abc", ""));
        Assert.Equal("", LogMascara.Aplicar("", @"C:\Users\maria"));
    }

    // RF26, docs/08 - ponta a ponta com o logger real
    [Fact]
    public void CreateLogger_NaoGravaPerfilDoUsuarioNoArquivo()
    {
        var perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(perfil))
            return; // perfil indisponivel neste ambiente: nada a mascarar

        var logsDir = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(logsDir);
        try
        {
            var logger = LoggingConfigurator.CreateLogger(logsDir);

            logger.Information("Banco em {Caminho}", perfil + @"\AppData\banco.db");
            logger.Error(new IOException("falha em " + perfil + @"\x.jpg"), "Falha");
            (logger as IDisposable)?.Dispose();

            var arquivo = Assert.Single(Directory.GetFiles(logsDir, "varthex-comanda-*.log"));
            var conteudo = File.ReadAllText(arquivo);
            Assert.DoesNotContain(perfil, conteudo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(@"\" + Environment.UserName + @"\", conteudo, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("%USERPROFILE%", conteudo);
            Assert.Contains("IOException", conteudo);
            Assert.Contains("Falha", conteudo);
        }
        finally
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }
}
