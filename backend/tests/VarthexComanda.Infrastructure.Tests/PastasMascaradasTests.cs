using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Backup;
using VarthexComanda.Infrastructure.Logging;
using VarthexComanda.Infrastructure.Storage;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

// RF26: alem do perfil, a pasta de dados fora do perfil (%DADOS%) e a pasta de backup externo
// (%PASTA_BACKUP_EXTERNA%) nao podem aparecer no log.
public class PastasMascaradasTests
{
    private const string Perfil = @"C:\Users\maria";

    private static PastasMascaradas Pastas(params (string token, string caminho)[] itens)
    {
        var pastas = new PastasMascaradas();
        foreach (var (token, caminho) in itens)
            pastas.Adicionar(token, caminho);
        return pastas;
    }

    [Fact]
    public void Aplicar_TrocaPastaRegistradaPeloToken()
    {
        var pastas = Pastas((PastasMascaradas.TokenPastaBackupExterna, @"D:\Backups Cliente"));

        var r = LogMascara.Aplicar(@"Sem acesso a D:\Backups Cliente\x.db", Perfil, pastas);

        Assert.Equal(@"Sem acesso a %PASTA_BACKUP_EXTERNA%\x.db", r);
    }

    [Fact]
    public void Aplicar_AceitaBarraNormalMaiusculasEBarrasDobradas()
    {
        var pastas = Pastas((PastasMascaradas.TokenDados, @"D:\dados"));

        Assert.Equal("%DADOS%/x", LogMascara.Aplicar("d:/DADOS/x", Perfil, pastas));
        Assert.Equal(@"%DADOS%\\x", LogMascara.Aplicar(@"D:\\dados\\x", Perfil, pastas));
    }

    [Theory]
    [InlineData(@"D:\dados2\x")]
    [InlineData(@"D:\dados.old\x")]
    [InlineData(@"D:\dados-antigos\x")]
    [InlineData(@"D:\dados_b\x")]
    [InlineData(@"D:\dadosx\x")]
    public void Aplicar_NaoCasaPastaComPrefixoIgual(string texto)
    {
        var pastas = Pastas((PastasMascaradas.TokenDados, @"D:\dados"));

        Assert.Equal(texto, LogMascara.Aplicar(texto, Perfil, pastas));
    }

    [Theory]
    [InlineData("\"D:\\dados\"", "\"%DADOS%\"")]
    [InlineData(@"D:\dados, D:\dados;", "%DADOS%, %DADOS%;")]
    [InlineData(@"em D:\dados.", "em %DADOS%.")]
    [InlineData(@"D:\dados: negado", "%DADOS%: negado")]
    [InlineData(@"D:\dados\", @"%DADOS%\")]
    public void Aplicar_MascaraQuandoPastaTerminaEmDelimitador(string texto, string esperado)
    {
        var pastas = Pastas((PastasMascaradas.TokenDados, @"D:\dados"));

        Assert.Equal(esperado, LogMascara.Aplicar(texto, Perfil, pastas));
    }

    [Fact]
    public void Aplicar_CaminhoUnc_EMascaradoComAsBarrasIniciais()
    {
        var pastas = Pastas((PastasMascaradas.TokenPastaBackupExterna, @"\\servidor\backup"));

        Assert.Equal(@"erro em %PASTA_BACKUP_EXTERNA%\a.db", LogMascara.Aplicar(@"erro em \\servidor\backup\a.db", Perfil, pastas));
    }

    [Fact]
    public void Aplicar_PastaDentroDoPerfil_NaoDeixaResiduoDoNomeDaSubpasta()
    {
        var pastas = Pastas((PastasMascaradas.TokenPastaBackupExterna, @"C:\Users\maria\Meus Backups"));

        var r = LogMascara.Aplicar(@"C:\Users\maria\Meus Backups\a.db e C:\Users\maria\Outra", Perfil, pastas);

        Assert.Equal(@"%PASTA_BACKUP_EXTERNA%\a.db e %USERPROFILE%\Outra", r);
    }

    [Fact]
    public void Aplicar_PastaMaisEspecificaVemAntesDaMaisGenerica()
    {
        var pastas = Pastas(
            (PastasMascaradas.TokenDados, @"D:\dados"),
            (PastasMascaradas.TokenPastaBackupExterna, @"D:\dados\externo"));

        var r = LogMascara.Aplicar(@"D:\dados\externo\a.db e D:\dados\banco.db", Perfil, pastas);

        Assert.Equal(@"%PASTA_BACKUP_EXTERNA%\a.db e %DADOS%\banco.db", r);
    }

    [Fact]
    public void Adicionar_RaizDeUnidadeOuVazio_EIgnorado()
    {
        var pastas = Pastas((PastasMascaradas.TokenDados, @"D:\"), (PastasMascaradas.TokenDados, ""), (PastasMascaradas.TokenDados, "   "));

        Assert.Equal(@"D:\x", LogMascara.Aplicar(@"D:\x", Perfil, pastas));
    }

    [Fact]
    public void Adicionar_DepoisDeCriadaAMascara_PassaAValerParaOsProximosEventos()
    {
        var pastas = new PastasMascaradas();
        var mascara = LogMascara.CriarMascara(Perfil, pastas);
        Assert.Equal(@"E:\ext\a", mascara(@"E:\ext\a"));

        pastas.Adicionar(PastasMascaradas.TokenPastaBackupExterna, @"E:\ext");

        Assert.Equal(@"%PASTA_BACKUP_EXTERNA%\a", mascara(@"E:\ext\a"));
    }

    [Fact]
    public void Adicionar_MesmaPastaDuasVezes_ContinuaFuncionando()
    {
        var pastas = Pastas((PastasMascaradas.TokenDados, @"D:\dados"), (PastasMascaradas.TokenDados, @"d:/DADOS/"));

        Assert.Equal(@"%DADOS%\x", LogMascara.Aplicar(@"D:\dados\x", Perfil, pastas));
    }

    [Fact]
    public void AdicionarDados_ForaDoPerfil_Registra()
    {
        var pastas = new PastasMascaradas();

        pastas.AdicionarDados(@"D:\dados", Perfil);

        Assert.Equal(@"%DADOS%\data\x.db", LogMascara.Aplicar(@"D:\dados\data\x.db", Perfil, pastas));
    }

    [Fact]
    public void AdicionarDados_DentroDoPerfil_NaoRegistraEDeixaOPerfilCobrir()
    {
        var pastas = new PastasMascaradas();

        pastas.AdicionarDados(@"C:\Users\maria\AppData\Local\VarthexComanda", Perfil);

        Assert.Equal(
            @"%USERPROFILE%\AppData\Local\VarthexComanda\data",
            LogMascara.Aplicar(@"C:\Users\maria\AppData\Local\VarthexComanda\data", Perfil, pastas));
    }

    [Fact]
    public void AdicionarDados_PerfilDeOutroUsuarioComMesmoPrefixo_TrataComoForaDoPerfil()
    {
        var pastas = new PastasMascaradas();

        pastas.AdicionarDados(@"C:\Users\maria2\dados", Perfil);

        Assert.Equal(@"%DADOS%\x", LogMascara.Aplicar(@"C:\Users\maria2\dados\x", Perfil, pastas));
    }

    [Fact]
    public void AdicionarEAplicarEmParalelo_NaoQuebra()
    {
        var pastas = new PastasMascaradas();

        Parallel.For(0, 200, i =>
        {
            pastas.Adicionar(PastasMascaradas.TokenPastaBackupExterna, $@"D:\pasta{i % 10}\sub");
            var r = pastas.Aplicar($@"x D:\pasta{i % 10}\sub\a.db");
            Assert.StartsWith("x ", r);
        });

        Assert.Equal(@"%PASTA_BACKUP_EXTERNA%\a.db", pastas.Aplicar(@"D:\pasta3\sub\a.db"));
    }

    // ---- ponta a ponta com o logger real e o EfBackupService/CriarBackupAutomatico reais ----

    [Fact]
    public void CreateLogger_PastaDeDadosForaDoPerfil_NaoApareceNoArquivo()
    {
        var pastas = new PastasMascaradas();
        pastas.AdicionarDados(@"D:\dados-do-cliente", Perfil);

        var conteudo = LogMascaraTests.Registrar(
            logger => logger.Error(new IOException(@"sem acesso a D:\dados-do-cliente\data\x.db"), "Falha em {Operacao} {Caminho}", "X", @"D:\dados-do-cliente\logs"),
            pastas: pastas);

        Assert.DoesNotContain("dados-do-cliente", conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(@"%DADOS%\data\x.db", conteudo);
        Assert.Contains(@"%DADOS%\logs", conteudo);
    }

    private sealed class ConfiguracaoComPasta : IConfiguracaoRepository
    {
        private readonly string _pasta;

        public ConfiguracaoComPasta(string pasta) => _pasta = pasta;

        public string? ObterValor(string chave) => chave == "backup.pasta_externa" ? _pasta : null;

        public void Definir(string chave, string valor, DateTime atualizadoEm)
        {
        }
    }

    private sealed class RegistrosNulos : IBackupRegistroRepository
    {
        public void Registrar(BackupRegistro registro)
        {
        }

        public IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade) => [];

        public bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc) => false;
    }

    private sealed class RelogioFixo : VarthexComanda.Application.Abstractions.IClock
    {
        public DateTime UtcNow => new(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);
    }

    // RF26: falha de I/O na copia externa automatica traz o caminho da pasta na mensagem do erro;
    // o arquivo de log real nao pode contê-lo.
    [Fact]
    public void BackupAutomaticoComFalhaNaPastaExterna_NaoGravaOCaminhoDaPastaNoLog()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "VarthexComandaTests_" + Guid.NewGuid());
        var paths = new AppPaths(raiz);
        paths.EnsureCreated();
        try
        {
            // um ARQUIVO no lugar da pasta externa: CreateDirectory falha e a mensagem cita o caminho
            var pastaExterna = Path.Combine(raiz, "cliente-secreto-backup");
            File.WriteAllText(pastaExterna, "x");
            var pastas = new PastasMascaradas();
            var relogio = new RelogioFixo();

            string? mensagemBruta = null;
            var conteudo = LogMascaraTests.Registrar(logger =>
            {
                var registros = new RegistrosNulos();
                var servico = new EfBackupService(paths, registros, relogio, logger: logger, pastasMascaradas: pastas);
                mensagemBruta = servico.CriarBackupExterno(pastaExterna).Erros.SingleOrDefault();

                var caso = new CriarBackupAutomatico(
                    servico, registros, relogio, new ObterConfiguracao(new ConfiguracaoComPasta(pastaExterna)), logger);
                caso.Executar(incondicional: true);
            }, perfil: @"C:\Users\zz-perfil-inexistente", pastas: pastas);

            // a mensagem original do erro de fato contem o caminho (o teste nao e vazio)
            Assert.Contains("cliente-secreto-backup", mensagemBruta);
            Assert.DoesNotContain("cliente-secreto-backup", conteudo, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("%PASTA_BACKUP_EXTERNA%", conteudo);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(raiz, recursive: true); } catch { }
        }
    }
}
