using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Backup;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Storage;
using VarthexComanda.Infrastructure.Tests.Suporte;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Backup;

// Etapa 7: fotos (fotos\) entram no backup como .db.fotos.zip e voltam na restauracao.
public class EfBackupServiceFotosTests : IDisposable
{
    private readonly string _raizTeste;
    private readonly AppPaths _paths;
    private readonly ServiceProvider _provedor;
    private readonly RegistrosEmMemoria _registros = new();
    private readonly RelogioFalso _relogio = new();

    public EfBackupServiceFotosTests()
    {
        _raizTeste = Path.Combine(Path.GetTempPath(), $"varthex-backupfotos-tests-{Guid.NewGuid()}");
        _paths = new AppPaths(_raizTeste);
        _paths.EnsureCreated();

        var servicos = new ServiceCollection();
        servicos.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={_paths.DatabasePath};Foreign Keys=True"));
        _provedor = servicos.BuildServiceProvider();
        using var contexto = _provedor.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>().CreateDbContext();
        contexto.Database.Migrate();
    }

    public void Dispose()
    {
        _provedor.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_raizTeste)) Directory.Delete(_raizTeste, recursive: true);
    }

    private EfBackupService NovoServico(int retencao = 30, ColetorDeLog? coletor = null, long limiteRestauracao = 2L * 1024 * 1024 * 1024) =>
        new(_paths, _registros, _relogio, retencao, coletor?.Logger, null, limiteRestauracao);

    private static string[] NomesDasFotosNoZip(string caminhoZip)
    {
        using var zip = ZipFile.OpenRead(caminhoZip);
        return zip.Entries.Select(e => e.FullName).Where(n => n != "_manifest.txt").OrderBy(n => n).ToArray();
    }

    private string[] ZipsDaPasta(string pasta) =>
        Directory.GetFiles(pasta).Where(f => f.EndsWith(".fotos.zip")).OrderBy(f => f).ToArray();

    /// <summary>Muda o conjunto de fotos: o nome e o instante de escrita mudam a impressao digital.</summary>
    private void AvancarBiblioteca(string nome)
    {
        var caminho = CriarFoto(nome, nome);
        File.SetLastWriteTimeUtc(caminho, DateTime.UtcNow.AddMinutes(-Random.Shared.Next(1, 100000)));
    }

    private string CriarFoto(string nome, string conteudo = "conteudo")
    {
        var caminho = Path.Combine(_paths.FotosDirectory, nome);
        File.WriteAllText(caminho, conteudo);
        return caminho;
    }

    private static string CaminhoDb(Resultado<BackupRegistro> r) => Path.Combine(r.Valor!.Destino, r.Valor.Arquivo);

    private static void CriarZip(string caminho, Action<ZipArchive> preencher)
    {
        using var fluxo = File.Create(caminho);
        using var zip = new ZipArchive(fluxo, ZipArchiveMode.Create);
        preencher(zip);
    }

    private static void AdicionarEntrada(ZipArchive zip, string nome, string conteudo)
    {
        var entrada = zip.CreateEntry(nome);
        using var escritor = new StreamWriter(entrada.Open());
        escritor.Write(conteudo);
    }

    /// <summary>Um .db valido com um .fotos.zip ao lado, fora da pasta de backups gerenciada.</summary>
    private string CriarBackupComZip(Action<ZipArchive> preencher)
    {
        var pasta = Path.Combine(_raizTeste, "origem");
        Directory.CreateDirectory(pasta);
        var resultado = NovoServico().CriarBackupExterno(pasta);
        Assert.True(resultado.Sucesso);
        var db = CaminhoDb(resultado);
        var zip = db + ".fotos.zip";
        if (File.Exists(zip)) File.Delete(zip);
        CriarZip(zip, preencher);
        return db;
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackupGerenciado_ComTresFotos_CriaZipComExatamenteEssasEntradas()
    {
        CriarFoto("a.png"); CriarFoto("b.jpg"); CriarFoto("c.bmp");

        var resultado = NovoServico().CriarBackupGerenciado();

        Assert.True(resultado.Sucesso);
        var caminhoZip = CaminhoDb(resultado) + ".fotos.zip";
        Assert.True(File.Exists(caminhoZip));
        Assert.Equal(new[] { "a.png", "b.jpg", "c.bmp" }, NomesDasFotosNoZip(caminhoZip));
        using (var zip = ZipFile.OpenRead(caminhoZip))
        {
            Assert.NotNull(zip.GetEntry("_manifest.txt"));
        }

        Assert.Empty(Directory.GetFiles(_paths.BackupsDirectory, "*.tmp"));
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackupGerenciado_SemFotosOuPastaAusente_NaoCriaZip()
    {
        var servico = NovoServico();
        var vazia = servico.CriarBackupGerenciado();
        Directory.Delete(_paths.FotosDirectory);
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        var ausente = servico.CriarBackupGerenciado();

        Assert.True(vazia.Sucesso);
        Assert.True(ausente.Sucesso);
        Assert.Empty(Directory.GetFiles(_paths.BackupsDirectory, "*.zip"));
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackupGerenciado_ArquivoNaoImagemEPastas_NaoEntramNoZip()
    {
        CriarFoto("a.png");
        CriarFoto("x.txt");
        CriarFoto("y.png.exe");
        Directory.CreateDirectory(Path.Combine(_paths.FotosDirectory, "sub"));
        File.WriteAllText(Path.Combine(_paths.FotosDirectory, "sub", "z.png"), "z");

        var resultado = NovoServico().CriarBackupGerenciado();

        Assert.Equal(new[] { "a.png" }, NomesDasFotosNoZip(CaminhoDb(resultado) + ".fotos.zip"));
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackupGerenciado_SoArquivosNaoImagem_NaoCriaZip()
    {
        CriarFoto("x.txt");

        NovoServico().CriarBackupGerenciado();

        Assert.Empty(Directory.GetFiles(_paths.BackupsDirectory, "*.zip"));
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void Retencao_ZipsAlemDosTresMaisRecentesSaoPodadosEARetencaoDoDbNaoMuda()
    {
        var servico = NovoServico(retencao: 2);
        var criados = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            AvancarBiblioteca($"f{i}.png"); // biblioteca muda a cada backup: um zip novo por backup
            _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
            criados.Add(CaminhoDb(servico.CriarBackupGerenciado()));
        }

        Assert.Equal(criados.Skip(3).OrderBy(x => x), Directory.GetFiles(_paths.BackupsDirectory, "varthex-comanda-*.db").OrderBy(x => x));
        Assert.Equal(2, Directory.GetFiles(_paths.BackupsDirectory, "*.sha256").Length);
        // os 3 zips mais recentes ficam, mesmo o do backup 3 cujo .db ja foi removido pela retencao de 2
        Assert.Equal(criados.Skip(2).Select(c => c + ".fotos.zip").OrderBy(x => x), ZipsDaPasta(_paths.BackupsDirectory));
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackup_BibliotecaInalterada_NaoCriaNovoZip()
    {
        CriarFoto("a.png");
        var servico = NovoServico();

        var primeiro = servico.CriarBackupGerenciado();
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        var segundo = servico.CriarBackupGerenciado();
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        var terceiro = servico.CriarBackupExterno(Path.Combine(_raizTeste, "externa"));

        Assert.True(segundo.Sucesso);
        Assert.Equal(new[] { CaminhoDb(primeiro) + ".fotos.zip" }, ZipsDaPasta(_paths.BackupsDirectory));
        Assert.False(File.Exists(CaminhoDb(segundo) + ".fotos.zip"));
        // a comparacao e por pasta de destino: a externa ainda nao tinha zip
        Assert.True(File.Exists(CaminhoDb(terceiro) + ".fotos.zip"));
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackup_FotoAdicionadaOuSubstituida_CriaNovoZip()
    {
        CriarFoto("a.png", "um");
        File.SetLastWriteTimeUtc(Path.Combine(_paths.FotosDirectory, "a.png"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var servico = NovoServico();
        servico.CriarBackupGerenciado();

        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        CriarFoto("b.png");
        var aposAdicionar = servico.CriarBackupGerenciado();

        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        File.WriteAllText(Path.Combine(_paths.FotosDirectory, "b.png"), "conteudo-bem-maior");
        var aposSubstituir = servico.CriarBackupGerenciado();

        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        var semMudanca = servico.CriarBackupGerenciado();

        Assert.True(File.Exists(CaminhoDb(aposAdicionar) + ".fotos.zip"));
        Assert.True(File.Exists(CaminhoDb(aposSubstituir) + ".fotos.zip"));
        Assert.False(File.Exists(CaminhoDb(semMudanca) + ".fotos.zip"));
        Assert.Equal(3, ZipsDaPasta(_paths.BackupsDirectory).Length);
    }

    [Fact]
    [Trait("Requisito", "RF22")]
    public void PastaExterna_PodaSoZipsEMantemDbsEArquivosDoUsuario()
    {
        var externa = Path.Combine(_raizTeste, "externa");
        Directory.CreateDirectory(externa);
        File.WriteAllText(Path.Combine(externa, "notas.zip"), "do usuario");
        File.WriteAllText(Path.Combine(externa, "leia-me.txt"), "do usuario");
        var servico = NovoServico(retencao: 1);
        var dbs = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            AvancarBiblioteca($"f{i}.png");
            _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
            dbs.Add(CaminhoDb(servico.CriarBackupExterno(externa)));
        }

        Assert.Equal(dbs.OrderBy(x => x), Directory.GetFiles(externa, "varthex-comanda-*.db").OrderBy(x => x));
        Assert.Equal(5, Directory.GetFiles(externa, "*.sha256").Length);
        Assert.Equal(dbs.Skip(2).Select(d => d + ".fotos.zip").OrderBy(x => x), ZipsDaPasta(externa).Where(z => !z.EndsWith("notas.zip")));
        Assert.True(File.Exists(Path.Combine(externa, "notas.zip")));
        Assert.True(File.Exists(Path.Combine(externa, "leia-me.txt")));
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackup_ZipTemporarioAbandonado_SoOsComMaisDeUmaHoraSaem()
    {
        var antigo = Path.Combine(_paths.BackupsDirectory, "varthex-comanda-2020-01-01-000000.db.fotos.zip.tmp");
        var recente = Path.Combine(_paths.BackupsDirectory, "varthex-comanda-2020-01-02-000000.db.fotos.zip.tmp");
        var alheio = Path.Combine(_paths.BackupsDirectory, "outro.tmp");
        File.WriteAllText(antigo, "x"); File.WriteAllText(recente, "x"); File.WriteAllText(alheio, "x");
        File.SetLastWriteTimeUtc(antigo, DateTime.UtcNow.AddHours(-2));
        File.SetLastWriteTimeUtc(alheio, DateTime.UtcNow.AddHours(-2));

        var resultado = NovoServico().CriarBackupGerenciado();

        Assert.True(resultado.Sucesso);
        Assert.False(File.Exists(antigo));
        Assert.True(File.Exists(recente));
        Assert.True(File.Exists(alheio));
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackupGerenciado_FalhaNaRetencao_ContinuaSucessoComWarning()
    {
        var coletor = new ColetorDeLog();
        var servico = NovoServico(retencao: 1, coletor: coletor);
        var primeiro = servico.CriarBackupGerenciado();
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        // o .db mais antigo seria apagado pela retencao, mas esta travado
        using var trava = new FileStream(CaminhoDb(primeiro), FileMode.Open, FileAccess.Read, FileShare.None);

        var segundo = servico.CriarBackupGerenciado();

        Assert.True(segundo.Sucesso);
        Assert.True(File.Exists(CaminhoDb(segundo)));
        Assert.Single(coletor.Eventos, e => e.Level == LogEventLevel.Warning && ((ScalarValue)e.Properties["Operacao"]).Value as string == "AplicarRetencao");
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void Retencao_GlobDoDbNaoCasaComZipENaoTocaNoCorrompidoBak()
    {
        CriarFoto("a.png");
        var bruto = Path.Combine(_paths.BackupsDirectory, "corrompido-2026-01-01-000000.db.bak");
        File.WriteAllText(bruto, "bruto");
        var servico = NovoServico(retencao: 1);
        for (var i = 0; i < 3; i++)
        {
            _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
            servico.CriarBackupGerenciado();
        }

        var dbs = Directory.GetFiles(_paths.BackupsDirectory, "varthex-comanda-*.db");
        Assert.All(dbs, f => Assert.EndsWith(".db", f));
        Assert.Single(dbs);
        Assert.True(File.Exists(bruto));
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackupGerenciado_FotoTravada_ZipTemAsDemaisSemNomeNoLogEBackupOk()
    {
        var bloqueada = CriarFoto("a.png");
        CriarFoto("b.png");
        var coletor = new ColetorDeLog();
        using var trava = new FileStream(bloqueada, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var resultado = NovoServico(coletor: coletor).CriarBackupGerenciado();

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusBackup.Sucesso, resultado.Valor!.Status);
        Assert.True(File.Exists(CaminhoDb(resultado)));
        Assert.Equal(new[] { "b.png" }, NomesDasFotosNoZip(CaminhoDb(resultado) + ".fotos.zip"));
        Assert.Empty(Directory.GetFiles(_paths.BackupsDirectory, "*.tmp"));
        var evento = Assert.Single(coletor.Eventos, e => e.Properties.TryGetValue("Operacao", out var o) && ((ScalarValue)o).Value as string == "ZiparFotosDoBackup");
        Assert.Equal(LogEventLevel.Warning, evento.Level);
        Assert.DoesNotContain("a.png", evento.RenderMessage());
        Assert.Equal(StatusBackup.Sucesso, Assert.Single(_registros.ListarRecentes(5)).Status);
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackupGerenciado_TodasAsFotosTravadas_NaoCriaZipNemTemporario()
    {
        var a = CriarFoto("a.png");
        var b = CriarFoto("b.png");
        using var t1 = new FileStream(a, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var t2 = new FileStream(b, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var resultado = NovoServico().CriarBackupGerenciado();

        Assert.True(resultado.Sucesso);
        Assert.Empty(Directory.GetFiles(_paths.BackupsDirectory, "*.zip*"));
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackupGerenciado_FotoTravada_ProximoBackupTentaDeNovo()
    {
        var bloqueada = CriarFoto("a.png");
        CriarFoto("b.png");
        var servico = NovoServico();
        FileStream? trava = new FileStream(bloqueada, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        servico.CriarBackupGerenciado();
        trava.Dispose();
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);

        var segundo = servico.CriarBackupGerenciado();

        Assert.Equal(new[] { "a.png", "b.png" }, NomesDasFotosNoZip(CaminhoDb(segundo) + ".fotos.zip"));
    }

    [Fact]
    [Trait("Requisito", "RF22")]
    public void CriarBackupExterno_CopiaOZipDeFotosJuntoQuandoHaFotos()
    {
        CriarFoto("a.png");
        var pasta = Path.Combine(_raizTeste, "externa");

        var resultado = NovoServico().CriarBackupExterno(pasta);

        Assert.True(resultado.Sucesso);
        var db = CaminhoDb(resultado);
        Assert.StartsWith(pasta, db);
        Assert.True(File.Exists(db + ".fotos.zip"));
        Assert.Equal(new[] { "a.png" }, NomesDasFotosNoZip(db + ".fotos.zip"));
    }

    [Fact]
    [Trait("Requisito", "RF22")]
    public void CriarBackupExterno_SemFotos_NaoCriaZip()
    {
        var pasta = Path.Combine(_raizTeste, "externa");

        NovoServico().CriarBackupExterno(pasta);

        Assert.Empty(Directory.GetFiles(pasta, "*.zip"));
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_ComZip_ExtraiFotosSemApagarOutrasESobrescreveMesmoNome()
    {
        CriarFoto("a.png", "A-original");
        CriarFoto("b.png", "B-original");
        var backup = NovoServico().CriarBackupGerenciado();
        var caminhoDb = CaminhoDb(backup);

        File.Delete(Path.Combine(_paths.FotosDirectory, "a.png"));
        File.WriteAllText(Path.Combine(_paths.FotosDirectory, "b.png"), "B-alterada");
        CriarFoto("nova.png", "NOVA");
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(5);

        var resultado = NovoServico().RestaurarPara(caminhoDb);

        Assert.True(resultado.Sucesso);
        Assert.Equal("A-original", File.ReadAllText(Path.Combine(_paths.FotosDirectory, "a.png")));
        Assert.Equal("B-original", File.ReadAllText(Path.Combine(_paths.FotosDirectory, "b.png")));
        Assert.Equal("NOVA", File.ReadAllText(Path.Combine(_paths.FotosDirectory, "nova.png")));
        Assert.Empty(Directory.GetFiles(_paths.FotosDirectory, "*.restaurando"));
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_ZipComEntradaMaliciosa_SoGravaONomePuroDentroDeFotos()
    {
        var db = CriarBackupComZip(zip =>
        {
            AdicionarEntrada(zip, @"..\..\evil.png", "mau");
            AdicionarEntrada(zip, "../../evil2.png", "mau2");
            AdicionarEntrada(zip, "/abs/evil3.png", "mau3");
            AdicionarEntrada(zip, "ok.png", "ok");
            AdicionarEntrada(zip, "script.exe", "nao");
            AdicionarEntrada(zip, "pasta/", "");
        });

        var resultado = NovoServico().RestaurarPara(db);

        Assert.True(resultado.Sucesso);
        Assert.Equal(
            new[] { "evil.png", "evil2.png", "evil3.png", "ok.png" },
            Directory.GetFiles(_paths.FotosDirectory).Select(f => Path.GetFileName(f)).OrderBy(n => n).ToArray());
        Assert.False(File.Exists(Path.Combine(_raizTeste, "evil.png")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_raizTeste)!, "evil.png")));
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_EntradaAcimaDe10MbEIgnoradaEIgualA10MbEExtraida()
    {
        var db = CriarBackupComZip(zip =>
        {
            var grande = zip.CreateEntry("grande.png", CompressionLevel.SmallestSize);
            using (var s = grande.Open())
            {
                s.Write(new byte[10 * 1024 * 1024 + 1]);
            }

            var limite = zip.CreateEntry("limite.png", CompressionLevel.SmallestSize);
            using (var s = limite.Open())
            {
                s.Write(new byte[10 * 1024 * 1024]);
            }
        });

        var resultado = NovoServico().RestaurarPara(db);

        Assert.True(resultado.Sucesso);
        Assert.False(File.Exists(Path.Combine(_paths.FotosDirectory, "grande.png")));
        Assert.True(File.Exists(Path.Combine(_paths.FotosDirectory, "limite.png")));
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_ZipComMaisDe5000Entradas_ExtraiNoMaximo5000()
    {
        var db = CriarBackupComZip(zip =>
        {
            for (var i = 0; i < 5100; i++)
            {
                AdicionarEntrada(zip, $"p{i:D4}.png", "x");
            }
        });

        var resultado = NovoServico().RestaurarPara(db);

        Assert.True(resultado.Sucesso);
        var extraidas = Directory.GetFiles(_paths.FotosDirectory).Length;
        Assert.InRange(extraidas, 1, 5000);
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_LimiteTotalDeBytes_ParaDeExtrairEAvisa()
    {
        var db = CriarBackupComZip(zip =>
        {
            for (var i = 0; i < 6; i++)
            {
                AdicionarEntrada(zip, $"p{i}.png", "0123456789"); // 10 bytes cada
            }
        });
        var coletor = new ColetorDeLog();

        var resultado = NovoServico(coletor: coletor, limiteRestauracao: 25).RestaurarPara(db);

        Assert.True(resultado.Sucesso);
        var arquivos = Directory.GetFiles(_paths.FotosDirectory);
        Assert.Equal(2, arquivos.Length);
        Assert.True(arquivos.Sum(a => new FileInfo(a).Length) <= 25);
        Assert.Empty(Directory.GetFiles(_paths.FotosDirectory, "*.restaurando"));
        Assert.Contains(coletor.Eventos, e => e.Level == LogEventLevel.Warning
            && ((ScalarValue)e.Properties["Operacao"]).Value as string == "RestaurarFotosDoBackup");
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_DbSemZipProprio_UsaOZipMaisRecenteDaMesmaPasta()
    {
        CriarFoto("a.png", "A");
        var servico = NovoServico();
        var primeiro = servico.CriarBackupGerenciado(); // cria o zip
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        var segundo = servico.CriarBackupGerenciado();   // biblioteca inalterada: sem zip proprio
        Assert.False(File.Exists(CaminhoDb(segundo) + ".fotos.zip"));
        File.Delete(Path.Combine(_paths.FotosDirectory, "a.png"));
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(5);

        var resultado = NovoServico().RestaurarPara(CaminhoDb(segundo));

        Assert.True(resultado.Sucesso);
        Assert.Equal("A", File.ReadAllText(Path.Combine(_paths.FotosDirectory, "a.png")));
        Assert.True(File.Exists(CaminhoDb(primeiro) + ".fotos.zip"));
        Assert.False(File.Exists(Path.Combine(_paths.FotosDirectory, "_manifest.txt")));
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_ZipInvalido_RestauracaoDoBancoSegueOkComWarning()
    {
        var pasta = Path.Combine(_raizTeste, "origem");
        var backup = NovoServico().CriarBackupExterno(pasta);
        var db = CaminhoDb(backup);
        File.WriteAllText(db + ".fotos.zip", "isto nao e um zip");
        var coletor = new ColetorDeLog();

        var resultado = NovoServico(coletor: coletor).RestaurarPara(db);

        Assert.True(resultado.Sucesso);
        var aviso = Assert.Single(coletor.Eventos, e => e.Level == LogEventLevel.Warning);
        Assert.Equal("RestaurarFotosDoBackup", ((ScalarValue)aviso.Properties["Operacao"]).Value);
        Assert.DoesNotContain(coletor.Eventos, e => e.Level >= LogEventLevel.Error);
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_BackupInvalido_NaoExtraiFotos()
    {
        var invalido = Path.Combine(_raizTeste, "invalido.db");
        File.WriteAllText(invalido, "nao e um banco");
        CriarZip(invalido + ".fotos.zip", zip => AdicionarEntrada(zip, "z.png", "z"));

        var resultado = NovoServico().RestaurarPara(invalido);

        Assert.False(resultado.Sucesso);
        Assert.Empty(Directory.GetFiles(_paths.FotosDirectory));
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_SemZip_FuncionaComoAntesEPreservaFotos()
    {
        CriarFoto("a.png", "A");
        var pasta = Path.Combine(_raizTeste, "origem");
        var backup = NovoServico().CriarBackupExterno(pasta);
        var db = CaminhoDb(backup);
        File.Delete(db + ".fotos.zip");

        var resultado = NovoServico().RestaurarPara(db);

        Assert.True(resultado.Sucesso);
        Assert.Equal("A", File.ReadAllText(Path.Combine(_paths.FotosDirectory, "a.png")));
    }

    [Fact]
    [Trait("Requisito", "RF23")]
    public void Validar_DbComZipAoLado_ContinuaValidoEChecksumInalterado()
    {
        CriarFoto("a.png");
        var servico = NovoServico();
        var backup = servico.CriarBackupGerenciado();
        var db = CaminhoDb(backup);

        var relatorio = servico.Validar(db);

        Assert.True(File.Exists(db + ".fotos.zip"));
        Assert.True(relatorio.FormatoValido);
        Assert.True(relatorio.IntegridadeOk);
        Assert.True(relatorio.ChecksumConfere);
        Assert.Equal(backup.Valor!.Checksum, File.ReadAllText(db + ".sha256").Trim());
    }

    [Fact]
    [Trait("Requisito", "RF23")]
    public void Validar_ArquivoZipDeFotos_NaoEBackupValido()
    {
        CriarFoto("a.png");
        var servico = NovoServico();
        var db = CaminhoDb(servico.CriarBackupGerenciado());

        var relatorio = servico.Validar(db + ".fotos.zip");

        Assert.False(relatorio.FormatoValido);
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_DbSemZipProprio_EscolheZipAnteriorAoBackupAntesDaCopiaPreventiva()
    {
        CriarFoto("a.png", "A");
        var servico = NovoServico();
        servico.CriarBackupGerenciado();                       // zip com a.png
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        var segundo = servico.CriarBackupGerenciado();         // inalterado: sem zip proprio
        Assert.False(File.Exists(CaminhoDb(segundo) + ".fotos.zip"));
        // a biblioteca atual difere do zip mais recente: a copia preventiva criaria um zip NOVO (so com x.png)
        File.Delete(Path.Combine(_paths.FotosDirectory, "a.png"));
        CriarFoto("x.png", "X");
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(5);

        var resultado = NovoServico().RestaurarPara(CaminhoDb(segundo));

        Assert.True(resultado.Sucesso);
        Assert.Equal("A", File.ReadAllText(Path.Combine(_paths.FotosDirectory, "a.png")));
        Assert.True(File.Exists(Path.Combine(_paths.FotosDirectory, "x.png")));
        Assert.False(File.Exists(Path.Combine(_paths.Root, "restaurando-fotos.tmp")));
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_ZipProprioEOTerceiroMaisRecente_PodaDaCopiaPreventivaNaoOPerde()
    {
        var servico = NovoServico();
        var criados = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            AvancarBiblioteca($"f{i}.png");
            _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
            criados.Add(CaminhoDb(servico.CriarBackupGerenciado()));
        }

        Assert.Equal(3, ZipsDaPasta(_paths.BackupsDirectory).Length);
        // o zip do primeiro backup (so com f0) e o 3o mais recente; a biblioteca mudou, entao a preventiva cria um 4o e poda o 1o
        File.Delete(Path.Combine(_paths.FotosDirectory, "f0.png"));
        AvancarBiblioteca("f9.png");
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(5);

        var resultado = NovoServico().RestaurarPara(criados[0]);

        Assert.True(resultado.Sucesso);
        Assert.False(File.Exists(criados[0] + ".fotos.zip")); // foi mesmo podado pela preventiva
        Assert.Equal("f0.png", File.ReadAllText(Path.Combine(_paths.FotosDirectory, "f0.png")));
    }

    [Fact]
    [Trait("Requisito", "RF24")]
    public void RestaurarPara_DbAnteriorATodosOsZips_UsaOZipMaisAntigo()
    {
        var servico = NovoServico();
        var semFotos = servico.CriarBackupGerenciado();        // sem zip
        AvancarBiblioteca("f0.png");
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        servico.CriarBackupGerenciado();                       // zip mais antigo: f0
        AvancarBiblioteca("f1.png");
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        servico.CriarBackupGerenciado();                       // zip mais novo: f0 + f1
        foreach (var f in Directory.GetFiles(_paths.FotosDirectory)) File.Delete(f);
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(5);

        var resultado = NovoServico().RestaurarPara(CaminhoDb(semFotos));

        Assert.True(resultado.Sucesso);
        Assert.Equal(new[] { "f0.png" }, Directory.GetFiles(_paths.FotosDirectory).Select(f => Path.GetFileName(f)).ToArray());
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void CriarBackup_FotoRemovida_CriaNovoZipSoComAsRestantes()
    {
        CriarFoto("a.png");
        CriarFoto("b.png");
        var servico = NovoServico();
        servico.CriarBackupGerenciado();
        File.Delete(Path.Combine(_paths.FotosDirectory, "b.png"));
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);

        var segundo = servico.CriarBackupGerenciado();

        Assert.Equal(new[] { "a.png" }, NomesDasFotosNoZip(CaminhoDb(segundo) + ".fotos.zip"));
    }

    [Fact]
    [Trait("Requisito", "RF22")]
    public void PastaExterna_NomesParecidosComZipDeFotosNaoSaoTocados()
    {
        var externa = Path.Combine(_raizTeste, "externa");
        Directory.CreateDirectory(externa);
        var ferias = Path.Combine(externa, "ferias.fotos.zip");
        var quase = Path.Combine(externa, "varthex-comanda-x.zip");
        File.WriteAllText(ferias, "do usuario");
        File.WriteAllText(quase, "do usuario");
        var servico = NovoServico();
        for (var i = 0; i < 5; i++)
        {
            AvancarBiblioteca($"f{i}.png");
            _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
            servico.CriarBackupExterno(externa);
        }

        Assert.True(File.Exists(ferias));
        Assert.True(File.Exists(quase));
        Assert.Equal(3, Directory.GetFiles(externa, "varthex-comanda-*.db.fotos.zip").Length);
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void PodarZips_ZipTravadoNaoImpedeAPodaDosDemais()
    {
        CriarFoto("a.png");
        var nomes = Enumerable.Range(1, 5).Select(i => Path.Combine(_paths.BackupsDirectory, $"varthex-comanda-2020-01-0{i}-000000.db.fotos.zip")).ToArray();
        foreach (var n in nomes) File.WriteAllText(n, "falso");
        var coletor = new ColetorDeLog();
        // fica: novo + f5 + f4; a poda tenta f3 (travado), f2 e f1
        using var trava = new FileStream(nomes[2], FileMode.Open, FileAccess.Read, FileShare.None);

        var resultado = NovoServico(coletor: coletor).CriarBackupGerenciado();

        Assert.True(resultado.Sucesso);
        Assert.True(File.Exists(nomes[2]));
        Assert.False(File.Exists(nomes[1]));
        Assert.False(File.Exists(nomes[0]));
        var aviso = Assert.Single(coletor.Eventos, e => e.Properties.TryGetValue("Operacao", out var o) && ((ScalarValue)o).Value as string == "PodarZipsDeFotos");
        Assert.Equal(LogEventLevel.Warning, aviso.Level);
        Assert.Null(aviso.Exception);
    }

    [Fact]
    [Trait("Requisito", "RF21")]
    public void Retencao_DbTravadoNaoImpedeARemocaoDosDemaisAntigos()
    {
        var servico = NovoServico();
        var b0 = servico.CriarBackupGerenciado();
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        var b1 = servico.CriarBackupGerenciado();
        _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
        var coletor = new ColetorDeLog();
        using var trava = new FileStream(CaminhoDb(b1), FileMode.Open, FileAccess.Read, FileShare.None);

        var b2 = NovoServico(retencao: 1, coletor: coletor).CriarBackupGerenciado();

        Assert.True(b2.Sucesso);
        Assert.True(File.Exists(CaminhoDb(b1)));   // travado: fica
        Assert.False(File.Exists(CaminhoDb(b0)));  // o mais antigo saiu mesmo assim
        Assert.False(File.Exists(CaminhoDb(b0) + ".sha256"));
        Assert.Single(coletor.Eventos, e => e.Properties.TryGetValue("Operacao", out var o) && ((ScalarValue)o).Value as string == "AplicarRetencao");
    }

    private sealed class RelogioFalso : VarthexComanda.Application.Abstractions.IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class RegistrosEmMemoria : IBackupRegistroRepository
    {
        private readonly List<BackupRegistro> _lista = new();

        public void Registrar(BackupRegistro registro)
        {
            registro.Id = _lista.Count + 1;
            _lista.Add(registro);
        }

        public IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade) =>
            _lista.OrderByDescending(r => r.CriadoEm).Take(quantidade).ToList();

        public bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc) => false;
    }
}
