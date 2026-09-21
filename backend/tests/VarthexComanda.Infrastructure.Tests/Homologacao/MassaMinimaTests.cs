using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Backup;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Backup;
using VarthexComanda.Infrastructure.Configuracao;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using VarthexComanda.Infrastructure.Storage;
using VarthexComanda.MassaMinima;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Homologacao;

// Etapa 8 / tarefa 2 — gerador da "massa minima" (docs/docs/09-testes-aceitacao.md, "Massa minima").
// Toda geracao acontece em pastas temporarias novas; a pasta real (%LOCALAPPDATA%\VarthexComanda) so
// aparece como TEXTO nos testes de recusa, que nao criam nem leem nada nela.
public sealed class MassaMinimaTests : IClassFixture<MassaMinimaTests.MassaGerada>, IDisposable
{
    private static readonly DateTime Agora = new(2026, 9, 21, 15, 0, 0, DateTimeKind.Utc); // 12:00 em Brasilia

    /// <summary>Uma unica geracao (segundos) compartilhada pelos testes de conteudo.</summary>
    public sealed class MassaGerada : IDisposable
    {
        public MassaGerada()
        {
            Raiz = Path.Combine(Path.GetTempPath(), $"varthex-massa-{Guid.NewGuid():N}");
            Resumo = MassaMinimaGerador.Gerar(Raiz, Agora);
            Paths = new AppPaths(Raiz);
        }

        public string Raiz { get; }
        public AppPaths Paths { get; }
        public ResumoMassa Resumo { get; }

        public void Dispose() => Apagar(Raiz);
    }

    private readonly MassaGerada _massa;
    private readonly List<string> _pastasExtras = new();

    public MassaMinimaTests(MassaGerada massa) => _massa = massa;

    public void Dispose()
    {
        foreach (var pasta in _pastasExtras) Apagar(pasta);
    }

    private string NovaPastaTemporaria()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"varthex-massa-{Guid.NewGuid():N}");
        _pastasExtras.Add(pasta);
        return pasta;
    }

    private static void Apagar(string pasta)
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(pasta, recursive: true); } catch { /* limpeza best-effort */ }
    }

    private VarthexComandaDbContext Abrir(string raiz)
    {
        var opcoes = new DbContextOptionsBuilder<VarthexComandaDbContext>()
            .UseSqlite($"Data Source={new AppPaths(raiz).DatabasePath};Foreign Keys=True;Pooling=False")
            .Options;
        return new VarthexComandaDbContext(opcoes);
    }

    private static T Escalar<T>(string caminho, string sql)
    {
        using var conexao = new SqliteConnection($"Data Source={caminho};Pooling=False");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        return (T)Convert.ChangeType(comando.ExecuteScalar()!, typeof(T));
    }

    private static EfBackupService ServicoDeBackup(AppPaths paths) =>
        new(paths, new NaoRegistraBackups(), new RelogioFixo());

    // ---- conteudo ---------------------------------------------------------------------------------

    [Fact]
    public void ContagensExatasDoDesign_Categorias_Produtos_NumerosDeComanda()
    {
        var r = _massa.Resumo;
        Assert.Equal((5, 1), (r.Categorias, r.CategoriasInativas));
        Assert.Equal((30, 1), (r.Produtos, r.ProdutosInativos));
        Assert.Equal(20, r.NumerosDeComanda);
        Assert.Equal(20, r.ProdutosComFoto);

        // o resumo nao e um contador interno: confere direto no banco, e a configuracao pelo repositorio real
        using var db = Abrir(_massa.Raiz);
        Assert.Equal(5, db.Categorias.Count());
        Assert.Equal(1, db.Categorias.Count(c => !c.Ativo));
        Assert.Equal(30, db.Produtos.Count());
        Assert.Equal(1, db.Produtos.Count(p => !p.Ativo));
        Assert.Equal("Descontinuados", db.Categorias.Single(c => !c.Ativo).Nome);
        Assert.Equal("20", new EfConfiguracaoRepository(new FabricaSimples(_massa.Raiz)).ObterValor("comandas.quantidade_maxima"));
        Assert.All(db.Produtos, p => Assert.True(p.PrecoCentavos is >= 100 and <= 10000));
    }

    [Fact]
    public void Fotos_SaoPngsSinteticosComNomeGuidEmFotos_EOsDemaisProdutosFicamSemFoto()
    {
        using var db = Abrir(_massa.Raiz);
        var comFoto = db.Produtos.Where(p => p.FotoArquivo != null).Select(p => p.FotoArquivo!).ToList();
        Assert.Equal(20, comFoto.Count);
        Assert.Equal(10, db.Produtos.Count(p => p.FotoArquivo == null)); // exercita o placeholder

        foreach (var nome in comFoto)
        {
            Assert.EndsWith(".png", nome);
            Assert.True(Guid.TryParseExact(Path.GetFileNameWithoutExtension(nome), "N", out _), nome);
            var caminho = Path.Combine(_massa.Paths.FotosDirectory, nome);
            Assert.True(File.Exists(caminho), nome);
            var bytes = File.ReadAllBytes(caminho);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes[..8]);
            Assert.True(ArquivoFotoStorage.ExtensaoPermitida(nome));
        }

        // nenhuma foto orfa: os arquivos da pasta sao exatamente os referenciados
        var arquivos = Directory.GetFiles(_massa.Paths.FotosDirectory).Select(f => Path.GetFileName(f)!).OrderBy(n => n).ToList();
        Assert.Equal(comFoto.OrderBy(n => n).ToList(), arquivos);
    }

    [Fact]
    public void ComandasAbertas_TemPerfisDiferentes_UmItem_MuitosItens_ItensRepetidos()
    {
        var abertas = new EfComandaRepository(new FabricaSimples(_massa.Raiz)).ListarAbertas();
        Assert.True(abertas.Count >= 3);
        Assert.Equal(5, abertas.Count);
        Assert.All(abertas, c => Assert.InRange(c.Numero, 1, 20));

        var repositorio = new EfComandaRepository(new FabricaSimples(_massa.Raiz));
        var detalhes = abertas.Select(c => repositorio.BuscarComItens(c.Id)!).ToList();
        var linhas = detalhes.Select(d => d.Itens.Count).ToList();

        Assert.Contains(0, linhas);                                   // vazia
        Assert.Contains(1, linhas);                                   // 1 item
        Assert.Contains(linhas, n => n >= 8);                         // muitos itens
        Assert.True(linhas.Distinct().Count() >= 3, "perfis diferentes");

        // itens repetidos: o app soma na mesma linha (X-Burger adicionado 3 vezes = quantidade 3)
        var repetidos = detalhes.Single(d => d.Comanda.Numero == 12);
        Assert.Equal(3, repetidos.Itens.Single(i => i.NomeProduto == "X-Burger").Quantidade);
        Assert.Equal(2, repetidos.Itens.Single(i => i.NomeProduto == "Batata Frita").Quantidade);
        Assert.All(detalhes, d => Assert.Equal(d.Itens.Sum(i => i.SubtotalCentavos), d.Comanda.TotalCentavos));
        Assert.Equal(_massa.Resumo.PerfisDasAbertas.Select(p => p.Numero), abertas.Select(c => c.Numero));
    }

    [Fact]
    public void Historico_TemNoventaDiasDeVendas_EmHorarioComercialDeBrasilia_ComFechamentosEAbandonos()
    {
        var r = _massa.Resumo;
        Assert.True(r.DiasDeHistorico >= 90);
        Assert.Equal(90, r.DiasComVendas); // ha venda em todos os dias do periodo
        Assert.True(r.Vendas >= 90 * 3);
        Assert.True(r.ComandasCanceladas > 0); // comandas abandonadas

        using var db = Abrir(_massa.Raiz);
        var vendas = db.Vendas.AsNoTracking().ToList();
        Assert.Equal(r.Vendas, vendas.Count);
        Assert.All(vendas, v =>
        {
            Assert.Equal(StatusVenda.Concluida, v.Status);
            Assert.True(v.FinalizadaEm < Agora);
            Assert.True(v.FinalizadaEm >= Agora.AddDays(-91));
            var local = FusoBrasilia.ParaLocal(v.FinalizadaEm);
            Assert.InRange(local.Hour, 10, 22); // horario comercial (10:00 a 22:59) — armazenado em UTC
        });

        var comandasFechadas = db.Comandas.Count(c => c.Status == StatusComanda.Fechada);
        Assert.Equal(vendas.Count, comandasFechadas);

        // totais coerentes: cada venda = soma dos itens = total da comanda
        foreach (var venda in vendas.Take(200))
        {
            var soma = db.ItensComanda.Where(i => i.ComandaId == venda.ComandaId).Sum(i => i.SubtotalCentavos);
            Assert.Equal(venda.TotalCentavos, soma);
            Assert.Equal(venda.TotalCentavos, db.Comandas.Single(c => c.Id == venda.ComandaId).TotalCentavos);
        }

        // ha vendas com item repetido (quantidade > 1 numa linha) e com varios itens
        var idsDeVendas = vendas.Select(v => v.ComandaId).ToHashSet();
        var itensDeVendas = db.ItensComanda.AsNoTracking().AsEnumerable().Where(i => idsDeVendas.Contains(i.ComandaId)).ToList();
        Assert.Equal(r.ItensEmVendas, itensDeVendas.Count);
        Assert.Contains(itensDeVendas, i => i.Quantidade > 1);
        Assert.Contains(itensDeVendas.GroupBy(i => i.ComandaId), g => g.Count() >= 4);
        Assert.Contains(itensDeVendas.GroupBy(i => i.ComandaId), g => g.Count() == 1);

        // o produto descontinuado vendeu enquanto estava ativo (historico preserva o nome)
        Assert.Contains(itensDeVendas, i => i.NomeProduto == "Torta de Limão");

        // o historico do app (repositorio real) enxerga tudo
        var todas = new EfVendaRepository(new FabricaSimples(_massa.Raiz))
            .ListarPorData(Agora.AddDays(-100), Agora);
        Assert.Equal(vendas.Count, todas.Count);
    }

    // ---- backups ----------------------------------------------------------------------------------

    [Fact]
    public void TresBackups_ValidoAntigoECorrompido_SaoClassificadosPeloValidarReal()
    {
        var r = _massa.Resumo;
        var servico = ServicoDeBackup(_massa.Paths);
        string Caminho(string nome) => Path.Combine(_massa.Paths.BackupsDirectory, nome);

        var bancos = Directory.GetFiles(_massa.Paths.BackupsDirectory, "varthex-comanda-*.db");
        Assert.Equal(3, bancos.Length);
        Assert.All(new[] { r.BackupValido, r.BackupAntigo, r.BackupCorrompido }, n => Assert.Matches(@"^varthex-comanda-\d{4}-\d{2}-\d{2}-\d{6}\.db$", n));
        Assert.Equal(3, new[] { r.BackupValido, r.BackupAntigo, r.BackupCorrompido }.Distinct().Count());

        // valido: aprovado, checksum confere, com as fotos (.fotos.zip) e o banco completo
        var valido = servico.Validar(Caminho(r.BackupValido));
        Assert.True(valido.Aprovado, valido.Motivo);
        Assert.True(valido.ChecksumConfere);
        Assert.True(File.Exists(Caminho(r.BackupValido) + ".fotos.zip"));
        Assert.Equal((long)r.Vendas, Escalar<long>(Caminho(r.BackupValido), "SELECT COUNT(*) FROM venda"));
        Assert.Equal(1L, Escalar<long>(Caminho(r.BackupValido), "SELECT COUNT(*) FROM pragma_table_info('produto') WHERE name = 'foto_arquivo'"));

        // antigo: reconhecido (versao compativel, checksum proprio confere) mas no esquema da InitialCreate
        var antigo = servico.Validar(Caminho(r.BackupAntigo));
        Assert.True(antigo.Aprovado, antigo.Motivo);
        Assert.True(antigo.ChecksumConfere);
        Assert.Equal(0L, Escalar<long>(Caminho(r.BackupAntigo), "SELECT COUNT(*) FROM pragma_table_info('produto') WHERE name = 'foto_arquivo'"));
        var aplicadas = Escalar<long>(Caminho(r.BackupAntigo), "SELECT COUNT(*) FROM \"__EFMigrationsHistory\"");
        Assert.Equal(1L, aplicadas);
        Assert.EndsWith("_InitialCreate", Escalar<string>(Caminho(r.BackupAntigo), "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\""));
        Assert.Equal(1L, Escalar<long>(Caminho(r.BackupAntigo), "SELECT COUNT(*) FROM venda"));
        Assert.True(Escalar<long>(Caminho(r.BackupAntigo), "SELECT COUNT(*) FROM __EFMigrationsHistory") < Abrir(_massa.Raiz).Database.GetMigrations().Count());

        // corrompido: rejeitado; o .sha256 existe mas nao e o do conteudo
        var corrompido = servico.Validar(Caminho(r.BackupCorrompido));
        Assert.False(corrompido.Aprovado);
        Assert.False(corrompido.FormatoValido);
        Assert.True(File.Exists(Caminho(r.BackupCorrompido) + ".sha256"));
        var declarado = File.ReadAllText(Caminho(r.BackupCorrompido) + ".sha256").Trim();
        var real = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Caminho(r.BackupCorrompido)))).ToLowerInvariant();
        Assert.NotEqual(real, declarado);
    }

    [Fact]
    public void BancoGerado_TemIntegridadeOk_SemMigracoesPendentes_ESemViolacaoDeChaveEstrangeira()
    {
        SqliteConnection.ClearAllPools();
        Assert.Equal("ok", Escalar<string>(_massa.Paths.DatabasePath, "PRAGMA integrity_check"));
        Assert.Equal(0L, Escalar<long>(_massa.Paths.DatabasePath, "SELECT COUNT(*) FROM pragma_foreign_key_check"));

        using var db = Abrir(_massa.Raiz);
        Assert.Empty(db.Database.GetPendingMigrations()); // Migrate() do app nao teria o que fazer
        Assert.Equal(db.Database.GetMigrations().Count(), db.Database.GetAppliedMigrations().Count());
        Assert.False(File.Exists(_massa.Paths.DatabasePath + "-wal"));
        Assert.False(File.Exists(_massa.Paths.DatabasePath + "-journal"));
    }

    // ---- reproducibilidade e --forcar -------------------------------------------------------------

    [Fact]
    public void GerarDeNovoComForcar_ReproduzOMesmoConteudo_EPreservaArquivosAlheios()
    {
        var pasta = NovaPastaTemporaria();
        Directory.CreateDirectory(Path.Combine(pasta, "data"));
        File.WriteAllText(Path.Combine(pasta, "data", "sobra.txt"), "lixo de execucao anterior");
        File.WriteAllText(Path.Combine(pasta, "meu-arquivo.txt"), "nao e do gerador");

        // sem --forcar: recusa e nao mexe em nada
        var ex = Assert.Throws<RecusaDeMassaException>(() => MassaMinimaGerador.Gerar(pasta, Agora));
        Assert.Contains("nao esta vazia", ex.Message);
        Assert.True(File.Exists(Path.Combine(pasta, "data", "sobra.txt")));

        var segundo = MassaMinimaGerador.Gerar(pasta, Agora, forcar: true);

        Assert.True(File.Exists(Path.Combine(pasta, "meu-arquivo.txt")));       // fora dos 4 subdiretorios: intocado
        Assert.False(File.Exists(Path.Combine(pasta, "data", "sobra.txt")));    // subdiretorio do app: refeito
        var primeiro = _massa.Resumo;
        Assert.Equal(primeiro with { PastaRaiz = "", PerfisDasAbertas = [] }, segundo with { PastaRaiz = "", PerfisDasAbertas = [] });
        Assert.Equal(primeiro.PerfisDasAbertas, segundo.PerfisDasAbertas);

        // mesmas fotos (nomes e bytes) e mesmas vendas (numero, total, instante)
        string Impressao(string raiz)
        {
            var paths = new AppPaths(raiz);
            var fotos = Directory.GetFiles(paths.FotosDirectory).OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => Path.GetFileName(f) + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
            using var db = Abrir(raiz);
            var vendas = db.Vendas.AsNoTracking().OrderBy(v => v.Numero)
                .Select(v => $"{v.Numero}|{v.TotalCentavos}|{v.FinalizadaEm.Ticks}").ToList();
            return string.Join("\n", fotos) + "\n--\n" + string.Join("\n", vendas);
        }
        Assert.Equal(Impressao(_massa.Raiz), Impressao(pasta));
    }

    // ---- seguranca --------------------------------------------------------------------------------

    public static IEnumerable<object[]> CaminhosProibidos()
    {
        var real = SegurancaDaSaida.PastaRealDosDados();
        var local = Path.GetDirectoryName(real)!;
        yield return new object[] { real };
        yield return new object[] { real + Path.DirectorySeparatorChar };
        yield return new object[] { real.ToUpperInvariant() };
        yield return new object[] { real.ToLowerInvariant() };
        yield return new object[] { Path.Combine(real, "data") };
        yield return new object[] { Path.Combine(real, "backups", "novo") };
        yield return new object[] { Path.Combine(real, "..", "VarthexComanda", "x") };  // desvio por ".."
        yield return new object[] { Path.Combine(local, "VarthexComanda", ".", "logs") };
        yield return new object[] { local };                                            // contem a pasta real
        yield return new object[] { Path.GetPathRoot(local)! };                         // raiz do disco
        yield return new object[] { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) };
        yield return new object[] { "" };
        yield return new object[] { "   " };
    }

    [Theory]
    [MemberData(nameof(CaminhosProibidos))]
    public void CaminhosProibidos_SaoRecusadosSemAcessoAoDisco(string pasta)
    {
        // a decisao e so por comparacao de caminhos (sem I/O): nada e criado nem lido nessas pastas
        Assert.NotNull(SegurancaDaSaida.MotivoDeRecusa(pasta));
        Assert.Throws<RecusaDeMassaException>(() => MassaMinimaGerador.Gerar(pasta, Agora));
        Assert.Throws<RecusaDeMassaException>(() => MassaMinimaGerador.Gerar(pasta, Agora, forcar: true)); // --forcar nao libera
    }

    [Fact]
    public void PastaTemporariaNova_ENomeComPrefixoParecido_NaoSaoRecusados()
    {
        Assert.Null(SegurancaDaSaida.MotivoDeRecusa(Path.Combine(Path.GetTempPath(), "varthex-massa-qualquer")));
        Assert.Null(SegurancaDaSaida.MotivoDeRecusa(Path.Combine(Path.GetTempPath(), "VarthexComandaOutra"))); // prefixo parecido != dentro
    }

    [Fact]
    public void PastaQueEUmArquivo_ERecusada()
    {
        var pasta = NovaPastaTemporaria();
        Directory.CreateDirectory(pasta);
        var arquivo = Path.Combine(pasta, "saida.txt");
        File.WriteAllText(arquivo, "x");
        Assert.Throws<RecusaDeMassaException>(() => MassaMinimaGerador.Gerar(arquivo, Agora));
    }

    [Fact]
    public void LinhaDeComando_RecusaComCodigoDeSaidaDiferenteDeZeroEMensagemClara()
    {
        var saida = new StringWriter();
        var erro = new StringWriter();

        Assert.Equal(2, LinhaDeComando.Executar(["--saida", SegurancaDaSaida.PastaRealDosDados()], saida, erro));
        Assert.Contains("pasta real dos dados", erro.ToString());
        Assert.Equal(string.Empty, saida.ToString());

        var naoVazia = NovaPastaTemporaria();
        Directory.CreateDirectory(naoVazia);
        File.WriteAllText(Path.Combine(naoVazia, "a.txt"), "x");
        erro = new StringWriter();
        Assert.Equal(2, LinhaDeComando.Executar(["--saida", naoVazia], saida, erro));
        Assert.Contains("--forcar", erro.ToString());

        erro = new StringWriter();
        Assert.Equal(1, LinhaDeComando.Executar([], saida, erro)); // sem --saida
        Assert.Contains("--saida", erro.ToString());
        Assert.Equal(1, LinhaDeComando.Executar(["--saida"], saida, new StringWriter()));
        Assert.Equal(1, LinhaDeComando.Executar(["--saida", naoVazia, "--agora", "ontem"], saida, new StringWriter()));
        Assert.Equal(1, LinhaDeComando.Executar(["--qualquer"], saida, new StringWriter()));
    }

    // ---- apoio ------------------------------------------------------------------------------------

    /// <summary>Fabrica simples para os repositorios reais lerem a massa (sem DI).</summary>
    private sealed class FabricaSimples : IDbContextFactory<VarthexComandaDbContext>
    {
        private readonly DbContextOptions<VarthexComandaDbContext> _opcoes;

        public FabricaSimples(string raiz) =>
            _opcoes = new DbContextOptionsBuilder<VarthexComandaDbContext>()
                .UseSqlite($"Data Source={new AppPaths(raiz).DatabasePath};Foreign Keys=True;Pooling=False")
                .Options;

        public VarthexComandaDbContext CreateDbContext() => new(_opcoes);
    }

    /// <summary>So validamos arquivos: o servico nao precisa gravar registros para chamar <c>Validar</c>.</summary>
    private sealed class NaoRegistraBackups : IBackupRegistroRepository
    {
        public void Registrar(BackupRegistro registro) { }
        public IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade) => [];
        public bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc) => false;
    }
}
