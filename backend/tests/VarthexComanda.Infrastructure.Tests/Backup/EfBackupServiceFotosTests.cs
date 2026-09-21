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

    private EfBackupService NovoServico(int retencao = 30, ColetorDeLog? coletor = null) =>
        new(_paths, _registros, _relogio, retencao, coletor?.Logger);

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
        using var zip = ZipFile.OpenRead(caminhoZip);
        Assert.Equal(new[] { "a.png", "b.jpg", "c.bmp" }, zip.Entries.Select(e => e.FullName).OrderBy(n => n).ToArray());
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

        using var zip = ZipFile.OpenRead(CaminhoDb(resultado) + ".fotos.zip");
        Assert.Equal("a.png", Assert.Single(zip.Entries).FullName);
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
    public void Retencao_RemoveTambemOsZipsDosRemovidosEMantemOsDosMantidos()
    {
        CriarFoto("a.png");
        var servico = NovoServico(retencao: 2);
        var criados = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            _relogio.UtcNow = _relogio.UtcNow.AddSeconds(1);
            criados.Add(CaminhoDb(servico.CriarBackupGerenciado()));
        }

        Assert.Equal(criados.Skip(2).OrderBy(x => x), Directory.GetFiles(_paths.BackupsDirectory, "varthex-comanda-*.db").OrderBy(x => x));
        Assert.Equal(
            criados.Skip(2).Select(c => c + ".fotos.zip").OrderBy(x => x),
            Directory.GetFiles(_paths.BackupsDirectory, "*.fotos.zip").OrderBy(x => x));
        Assert.Equal(2, Directory.GetFiles(_paths.BackupsDirectory, "*.sha256").Length);
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
    public void CriarBackupGerenciado_FalhaAoZipar_BackupDoDbOkSemZipParcialEComWarning()
    {
        var bloqueada = CriarFoto("a.png");
        CriarFoto("b.png");
        var coletor = new ColetorDeLog();
        using var trava = new FileStream(bloqueada, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var resultado = NovoServico(coletor: coletor).CriarBackupGerenciado();

        Assert.True(resultado.Sucesso);
        Assert.Equal(StatusBackup.Sucesso, resultado.Valor!.Status);
        Assert.True(File.Exists(CaminhoDb(resultado)));
        Assert.Empty(Directory.GetFiles(_paths.BackupsDirectory, "*.zip*"));
        var evento = Assert.Single(coletor.Eventos);
        Assert.Equal(LogEventLevel.Warning, evento.Level);
        Assert.Equal("ZiparFotosDoBackup", ((ScalarValue)evento.Properties["Operacao"]).Value);
        Assert.Equal(StatusBackup.Sucesso, Assert.Single(_registros.ListarRecentes(5)).Status);
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
        using var zip = ZipFile.OpenRead(db + ".fotos.zip");
        Assert.Equal("a.png", Assert.Single(zip.Entries).FullName);
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
    public void RestaurarPara_ZipBomba_IgnoraEntradaAcimaDe10MbECortaEm5000Entradas()
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

            for (var i = 0; i < 5100; i++)
            {
                AdicionarEntrada(zip, $"p{i:D4}.png", "x");
            }
        });

        var resultado = NovoServico().RestaurarPara(db);

        Assert.True(resultado.Sucesso);
        Assert.False(File.Exists(Path.Combine(_paths.FotosDirectory, "grande.png")));
        Assert.True(File.Exists(Path.Combine(_paths.FotosDirectory, "limite.png")));
        // no maximo 5000 entradas lidas: "grande" (ignorada) + "limite" + 4998 pequenas
        Assert.Equal(4999, Directory.GetFiles(_paths.FotosDirectory).Length);
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
