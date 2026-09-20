using VarthexComanda.Infrastructure.Logging;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class LogMascaraTests
{
    private const string Perfil = @"C:\Users\maria";

    // RF26, docs/08
    [Fact]
    public void Aplicar_TrocaPerfilPorVariavel()
    {
        var r = LogMascara.Aplicar(@"C:\Users\maria\AppData\Local\X", Perfil);
        Assert.Equal(@"%USERPROFILE%\AppData\Local\X", r);
    }

    [Fact]
    public void Aplicar_AceitaBarraNormalEMaiusculasDiferentes()
    {
        var r = LogMascara.Aplicar("c:/users/MARIA/Fotos/a.jpg", Perfil);
        Assert.Equal("%USERPROFILE%/Fotos/a.jpg", r);
    }

    [Fact]
    public void Aplicar_CaminhoComBarrasDobradas_EMascarado()
    {
        var r = LogMascara.Aplicar(@"C:\\Users\\maria\\AppData\\x", Perfil);
        Assert.Equal(@"%USERPROFILE%\\AppData\\x", r);
    }

    [Fact]
    public void Aplicar_NaoAlteraTextoSemOPerfil()
    {
        const string texto = @"Falha em D:\dados\banco.db";
        Assert.Equal(texto, LogMascara.Aplicar(texto, Perfil));
    }

    [Theory]
    [InlineData(@"C:\Users\maria2\x")]
    [InlineData(@"C:\Users\maria.silva\x")]
    [InlineData(@"C:\Users\maria-costa\x")]
    [InlineData(@"C:\Users\maria_b\x")]
    [InlineData(@"C:\Users\mariana\x")]
    public void Aplicar_NaoCasaOutroUsuarioComPrefixoIgual(string texto)
    {
        Assert.Equal(texto, LogMascara.Aplicar(texto, Perfil));
    }

    [Theory]
    [InlineData("\"C:\\Users\\maria\"", "\"%USERPROFILE%\"")]
    [InlineData(@"C:\Users\maria, C:\Users\maria;", "%USERPROFILE%, %USERPROFILE%;")]
    [InlineData(@"(C:\Users\maria)", "(%USERPROFILE%)")]
    [InlineData(@"em C:\Users\maria", "em %USERPROFILE%")]
    [InlineData(@"em C:\Users\maria.", "em %USERPROFILE%.")]
    [InlineData(@"C:\Users\maria: negado", "%USERPROFILE%: negado")]
    [InlineData(@"C:\Users\maria\", @"%USERPROFILE%\")]
    public void Aplicar_MascaraQuandoPerfilTerminaEmDelimitador(string texto, string esperado)
    {
        Assert.Equal(esperado, LogMascara.Aplicar(texto, Perfil));
    }

    [Fact]
    public void Aplicar_TrocaVariasOcorrencias()
    {
        var r = LogMascara.Aplicar(@"de C:\Users\maria\a para C:\Users\maria\b", Perfil);
        Assert.Equal(@"de %USERPROFILE%\a para %USERPROFILE%\b", r);
    }

    [Fact]
    public void Aplicar_PerfilVazioOuTextoVazio_NaoAltera()
    {
        Assert.Equal("abc", LogMascara.Aplicar("abc", ""));
        Assert.Equal("", LogMascara.Aplicar("", Perfil));
    }

    // RF26, docs/08 - ponta a ponta com o logger real e um perfil ficticio (independe da maquina)
    [Fact]
    public void CreateLogger_NaoGravaPerfilDoUsuarioNoArquivo()
    {
        var conteudo = Registrar(logger =>
        {
            logger.Information("Banco em {Caminho}", Perfil + @"\AppData\banco.db");
            logger.Error(new IOException("falha em " + Perfil + @"\x.jpg"), "Falha");
        });

        Assert.DoesNotContain(Perfil, conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(@"\maria\", conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"%USERPROFILE%\AppData\banco.db", conteudo);
        Assert.Contains("IOException", conteudo);
        Assert.Contains("Falha", conteudo);
        Assert.Contains(@"falha em %USERPROFILE%\x.jpg", conteudo);
    }

    [Fact]
    public void CreateLogger_TemplateComChavesLiterais_PreservaEMascara()
    {
        var conteudo = Registrar(logger => logger.Information("{{x}} {Caminho}", Perfil + @"\y"));

        Assert.Contains(@"{x} %USERPROFILE%\y", conteudo);
        Assert.DoesNotContain(Perfil, conteudo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateLogger_ExcecaoAninhadaEAgregada_MascaraTodosOsNiveis()
    {
        var interna = new FileNotFoundException("interna " + Perfil + @"\a.db");
        var agregada = new AggregateException("agregada " + Perfil + @"\b",
            new IOException("filha " + Perfil + @"\c", interna));
        var externa = new InvalidOperationException("externa " + Perfil + @"\d", agregada);

        var conteudo = Registrar(logger => logger.Error(externa, "Falha"));

        Assert.DoesNotContain(Perfil, conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("InvalidOperationException", conteudo);
        Assert.Contains("AggregateException", conteudo);
        Assert.Contains("FileNotFoundException", conteudo);
        Assert.Contains(@"interna %USERPROFILE%\a.db", conteudo);
        Assert.Contains(@"filha %USERPROFILE%\c", conteudo);
    }

    [Fact]
    public void CreateLogger_ExcecaoComStackTraceContendoPerfil_EMascarada()
    {
        var conteudo = Registrar(logger => logger.Error(new ExcecaoComStack(), "Falha"));

        Assert.DoesNotContain(Perfil, conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"at Foo.Bar() in %USERPROFILE%\src\Foo.cs:line 10", conteudo);
    }

    [Fact]
    public void CreateLogger_ExcecaoLancadaDeVerdade_MascaraPerfilNoStackTrace()
    {
        // O stack real contem o caminho do arquivo-fonte (informacao de build); usa-se como perfil o diretorio
        // do proprio arquivo de teste para provar que o stack passa pela mascara.
        var arquivoFonte = ArquivoFonteDoTeste();
        var pasta = Path.GetDirectoryName(arquivoFonte)!;
        Exception capturada;
        try { throw new InvalidOperationException("boom"); }
        catch (Exception ex) { capturada = ex; }
        Assert.Contains(pasta, capturada.StackTrace, StringComparison.OrdinalIgnoreCase);

        var conteudo = Registrar(logger => logger.Error(capturada, "Falha"), pasta);

        Assert.DoesNotContain(pasta, conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("%USERPROFILE%", conteudo);
    }

    private static string ArquivoFonteDoTeste([System.Runtime.CompilerServices.CallerFilePath] string caminho = "") => caminho;

    private static string Registrar(Action<Serilog.ILogger> escrever, string perfil = Perfil)
    {
        var logsDir = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        Directory.CreateDirectory(logsDir);
        try
        {
            var logger = LoggingConfigurator.CreateLogger(logsDir, perfilUsuario: perfil);
            escrever(logger);
            (logger as IDisposable)?.Dispose();

            var arquivo = Assert.Single(Directory.GetFiles(logsDir, "varthex-comanda-*.log"));
            return File.ReadAllText(arquivo);
        }
        finally
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }

    private sealed class ExcecaoComStack : Exception
    {
        public ExcecaoComStack() : base("erro") { }

        public override string StackTrace => @"   at Foo.Bar() in " + Perfil + @"\src\Foo.cs:line 10";
    }
}
