using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Events;
using VarthexComanda.Application.Backup;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Backup;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Tests.Suporte;
using VarthexComanda.Infrastructure.Storage;
using VarthexComanda.Infrastructure.Time;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Backup;

public class EfBackupServiceTests : IDisposable
{
    private readonly string _raizTeste;
    private readonly AppPaths _paths;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;
    private readonly FakeBackupRegistroRepositoryDeIntegracao _registros;

    public EfBackupServiceTests()
    {
        _raizTeste = Path.Combine(Path.GetTempPath(), $"varthex-backupservice-tests-{Guid.NewGuid()}");
        _paths = new AppPaths(_raizTeste);
        _paths.EnsureCreated();

        var servicos = new ServiceCollection();
        servicos.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={_paths.DatabasePath};Foreign Keys=True"));
        _provedor = servicos.BuildServiceProvider();
        _fabrica = _provedor.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();

        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Database.Migrate();
        }

        _registros = new FakeBackupRegistroRepositoryDeIntegracao();
    }

    public void Dispose()
    {
        _provedor.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_raizTeste)) Directory.Delete(_raizTeste, recursive: true);
    }

    [Fact]
    public void CriarBackupGerenciado_CriaArquivoDbEChecksumECompanheiro()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);

        var resultado = servico.CriarBackupGerenciado();

        Assert.True(resultado.Sucesso);
        var caminhoDb = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);
        Assert.True(File.Exists(caminhoDb));
        Assert.True(File.Exists(caminhoDb + ".sha256"));
        Assert.Equal(resultado.Valor.Checksum, File.ReadAllText(caminhoDb + ".sha256").Trim());
    }

    [Fact]
    public void CriarBackupGerenciado_RegistraSucessoNoRepositorio()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);

        servico.CriarBackupGerenciado();

        var registro = Assert.Single(_registros.ListarRecentes(10));
        Assert.Equal(StatusBackup.Sucesso, registro.Status);
        Assert.NotNull(registro.Checksum);
    }

    [Fact]
    public void CriarBackupGerenciado_BackupPassaNaVerificacaoDeIntegridade()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);

        var resultado = servico.CriarBackupGerenciado();

        var caminhoDb = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);
        using var conexao = new SqliteConnection($"Data Source={caminhoDb}");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "PRAGMA integrity_check";
        Assert.Equal("ok", (string?)comando.ExecuteScalar());
    }

    [Fact]
    public void CriarBackupGerenciado_MaisDeTresBackups_RetencaoMantemSomenteOsTresMaisRecentes()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio, retencaoMaxima: 3);

        for (var i = 0; i < 5; i++)
        {
            relogio.UtcNow = relogio.UtcNow.AddSeconds(1);
            servico.CriarBackupGerenciado();
        }

        var arquivos = Directory.GetFiles(_paths.BackupsDirectory, "varthex-comanda-*.db");
        Assert.Equal(3, arquivos.Length);
    }

    [Fact]
    public void CriarBackupExterno_NaoAplicaRetencaoNaPastaExterna()
    {
        var pastaExterna = Path.Combine(_raizTeste, "externa");
        Directory.CreateDirectory(pastaExterna);
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio, retencaoMaxima: 3);

        for (var i = 0; i < 5; i++)
        {
            relogio.UtcNow = relogio.UtcNow.AddSeconds(1);
            servico.CriarBackupExterno(pastaExterna);
        }

        var arquivos = Directory.GetFiles(pastaExterna, "varthex-comanda-*.db");
        Assert.Equal(5, arquivos.Length);
    }

    [Fact]
    public void Validar_ArquivoValido_TodasAsChecagensPassam()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var resultado = servico.CriarBackupGerenciado();
        var caminho = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);

        var relatorio = servico.Validar(caminho);

        Assert.True(relatorio.FormatoValido);
        Assert.True(relatorio.VersaoCompativel);
        Assert.True(relatorio.IntegridadeOk);
        Assert.True(relatorio.ChecksumConfere);
        Assert.True(relatorio.Aprovado);
    }

    [Fact]
    public void Validar_ArquivoNaoSqlite_FalhaNoFormato()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var caminhoInvalido = Path.Combine(_raizTeste, "nao-e-um-banco.db");
        File.WriteAllText(caminhoInvalido, "isto nao e um banco sqlite");

        var relatorio = servico.Validar(caminhoInvalido);

        Assert.False(relatorio.FormatoValido);
        Assert.False(relatorio.Aprovado);
        Assert.NotEmpty(relatorio.Motivo);
    }

    [Fact]
    public void Validar_ArquivoInexistente_FalhaNoFormato()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);

        var relatorio = servico.Validar(Path.Combine(_raizTeste, "nao-existe.db"));

        Assert.False(relatorio.FormatoValido);
        Assert.False(relatorio.Aprovado);
    }

    [Fact]
    public void Validar_MigracaoFuturaDesconhecida_FalhaNaVersao()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var resultado = servico.CriarBackupGerenciado();
        var caminho = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);

        using (var conexao = new SqliteConnection($"Data Source={caminho}"))
        {
            conexao.Open();
            using var comando = conexao.CreateCommand();
            comando.CommandText =
                "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('99999999999999_MigracaoFutura', '99.0.0')";
            comando.ExecuteNonQuery();
        }

        var relatorio = servico.Validar(caminho);

        Assert.True(relatorio.FormatoValido);
        Assert.False(relatorio.VersaoCompativel);
        Assert.False(relatorio.Aprovado);
    }

    [Fact]
    public void Validar_ArquivoCorrompido_FalhaNaIntegridade()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var resultado = servico.CriarBackupGerenciado();
        var caminho = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);

        SqliteConnection.ClearAllPools();
        using (var stream = new FileStream(caminho, FileMode.Open, FileAccess.Write))
        {
            stream.Seek(100, SeekOrigin.Begin);
            var lixo = new byte[200];
            new Random(42).NextBytes(lixo);
            stream.Write(lixo, 0, lixo.Length);
        }

        var relatorio = servico.Validar(caminho);

        Assert.True(relatorio.FormatoValido);
        Assert.True(relatorio.VersaoCompativel);
        Assert.False(relatorio.IntegridadeOk);
        Assert.False(relatorio.Aprovado);
    }

    [Fact]
    public void Validar_SemArquivoSha256Companheiro_ChecksumNaoVerificavelMasAprovado()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var resultado = servico.CriarBackupGerenciado();
        var caminho = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);
        File.Delete(caminho + ".sha256");

        var relatorio = servico.Validar(caminho);

        Assert.Null(relatorio.ChecksumConfere);
        Assert.True(relatorio.Aprovado);
    }

    [Fact]
    public void Validar_ChecksumSha256Adulterado_FalhaNoChecksumMesmoComDbValido()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var resultado = servico.CriarBackupGerenciado();
        var caminho = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);
        File.WriteAllText(caminho + ".sha256", "0000000000000000000000000000000000000000000000000000000000000000");

        var relatorio = servico.Validar(caminho);

        Assert.False(relatorio.ChecksumConfere);
        Assert.False(relatorio.Aprovado);
        Assert.NotEmpty(relatorio.Motivo);
    }

    [Fact]
    public void RestaurarPara_ArquivoValido_CriaCopiaPreventivaETrocaABaseAtiva()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);

        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Categorias.Add(new Categoria { Id = 0, Nome = "Original", Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
            contexto.SaveChanges();
        }
        var backupComOriginal = servico.CriarBackupGerenciado();
        var caminhoBackupOriginal = Path.Combine(backupComOriginal.Valor!.Destino, backupComOriginal.Valor.Arquivo);

        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Categorias.Add(new Categoria { Id = 0, Nome = "Nova", Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
            contexto.SaveChanges();
        }

        var resultado = servico.RestaurarPara(caminhoBackupOriginal);

        Assert.True(resultado.Sucesso);
        SqliteConnection.ClearAllPools();
        using (var contextoPosRestauracao = _fabrica.CreateDbContext())
        {
            var nomes = contextoPosRestauracao.Categorias.Select(c => c.Nome).ToList();
            Assert.Contains("Original", nomes);
            Assert.DoesNotContain("Nova", nomes);
        }
    }

    [Fact]
    public void RestaurarPara_ArquivoInvalido_NaoAlteraABaseAtiva()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Categorias.Add(new Categoria { Id = 0, Nome = "Preservada", Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
            contexto.SaveChanges();
        }
        var caminhoInvalido = Path.Combine(_raizTeste, "invalido.db");
        File.WriteAllText(caminhoInvalido, "nao e um banco");

        var resultado = servico.RestaurarPara(caminhoInvalido);

        Assert.False(resultado.Sucesso);
        SqliteConnection.ClearAllPools();
        using (var contexto = _fabrica.CreateDbContext())
        {
            Assert.Contains("Preservada", contexto.Categorias.Select(c => c.Nome).ToList());
        }
    }

    [Fact]
    public void RestaurarPara_ArquivoInexistente_NaoAlteraABaseAtiva()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Categorias.Add(new Categoria { Id = 0, Nome = "Preservada", Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
            contexto.SaveChanges();
        }
        var caminhoInexistente = Path.Combine(_raizTeste, "nao-existe.db");

        var resultado = servico.RestaurarPara(caminhoInexistente);

        Assert.False(resultado.Sucesso);
        SqliteConnection.ClearAllPools();
        using (var contexto = _fabrica.CreateDbContext())
        {
            Assert.Contains("Preservada", contexto.Categorias.Select(c => c.Nome).ToList());
        }
    }

    // Etapa 7, ruling 4: com o banco ativo corrompido a restauracao continua possivel;
    // o arquivo bruto e guardado em backups\corrompido-*.db.bak antes de ser substituido.
    [Theory]
    [InlineData("lixo")]
    [InlineData("truncado")]
    public void RestaurarPara_ComBancoAtivoCorrompido_RestauraEGuardaCopiaBruta(string tipoDeCorrupcao)
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Categorias.Add(new Categoria { Id = 0, Nome = "Original", Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
            contexto.SaveChanges();
        }
        var backupValido = servico.CriarBackupGerenciado();
        var caminhoBackup = Path.Combine(backupValido.Valor!.Destino, backupValido.Valor.Arquivo);

        SqliteConnection.ClearAllPools();
        var bytesCorrompidos = tipoDeCorrupcao == "lixo"
            ? System.Text.Encoding.UTF8.GetBytes(new string('x', 5000))
            : File.ReadAllBytes(_paths.DatabasePath).Take(4096 + 512).ToArray();
        File.WriteAllBytes(_paths.DatabasePath, bytesCorrompidos);

        var hashAntes = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(_paths.DatabasePath));
        var registrosAntes = _registros.ListarRecentes(100).Count;

        relogio.UtcNow = relogio.UtcNow.AddSeconds(5);
        var resultado = servico.RestaurarPara(caminhoBackup);

        Assert.True(resultado.Sucesso);
        // nada foi escrito no banco corrompido antes da copia bruta: SHA-256 identico ao original
        // e nenhuma tentativa de registrar falha de backup (o registro vive no banco corrompido)
        var copiaBrutaHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Assert.Single(Directory.GetFiles(_paths.BackupsDirectory, "corrompido-*.db.bak"))));
        Assert.Equal(hashAntes, copiaBrutaHash);
        Assert.Equal(registrosAntes, _registros.ListarRecentes(100).Count);
        // so o backup bom original: nenhum varthex-comanda-*.db extra (preventivo) foi criado
        Assert.Single(Directory.GetFiles(_paths.BackupsDirectory, "varthex-comanda-*.db"));
        using (var conexao = new SqliteConnection($"Data Source={_paths.DatabasePath};Pooling=False"))
        {
            conexao.Open();
            using var comando = conexao.CreateCommand();
            comando.CommandText = "PRAGMA integrity_check";
            Assert.Equal("ok", (string?)comando.ExecuteScalar());
        }
        SqliteConnection.ClearAllPools();
        using (var contexto = _fabrica.CreateDbContext())
        {
            Assert.Contains("Original", contexto.Categorias.Select(c => c.Nome).ToList());
        }

        var copiaBruta = Assert.Single(Directory.GetFiles(_paths.BackupsDirectory, "corrompido-*.db.bak"));
        Assert.Equal("corrompido-2026-09-18-120005.db.bak", Path.GetFileName(copiaBruta));
        Assert.Equal(bytesCorrompidos, File.ReadAllBytes(copiaBruta));
        Assert.Equal(Path.GetFileName(copiaBruta), resultado.Valor!.Arquivo);
    }

    [Fact]
    public void RestaurarPara_ComBancoAtivoCorrompido_CopiaBrutaImpossivel_CancelaSemAlterarABase()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var backupValido = servico.CriarBackupGerenciado();
        var caminhoBackup = Path.Combine(backupValido.Valor!.Destino, backupValido.Valor.Arquivo);
        SqliteConnection.ClearAllPools();
        var bytesCorrompidos = System.Text.Encoding.UTF8.GetBytes(new string('x', 5000));
        File.WriteAllBytes(_paths.DatabasePath, bytesCorrompidos);
        relogio.UtcNow = relogio.UtcNow.AddSeconds(5);
        // uma pasta ocupando o nome da copia bruta faz o File.Copy falhar
        Directory.CreateDirectory(Path.Combine(_paths.BackupsDirectory, "corrompido-2026-09-18-120005.db.bak"));

        var resultado = servico.RestaurarPara(caminhoBackup);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Não foi possível guardar uma cópia do banco atual; restauração cancelada.", resultado.Erros);
        Assert.Equal(bytesCorrompidos, File.ReadAllBytes(_paths.DatabasePath));
    }

    [Fact]
    public void RestaurarPara_ComBancoAtivoBloqueado_NaoTrataComoCorrupcaoECancelaSemAlterarNada()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var backupValido = servico.CriarBackupGerenciado();
        var caminhoBackup = Path.Combine(backupValido.Valor!.Destino, backupValido.Valor.Arquivo);
        SqliteConnection.ClearAllPools();
        var hashAntes = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(_paths.DatabasePath));
        relogio.UtcNow = relogio.UtcNow.AddSeconds(5);

        // outra conexao segura o banco com lock exclusivo (SQLITE_BUSY para quem tentar ler)
        using (var trava = new SqliteConnection($"Data Source={_paths.DatabasePath};Pooling=False"))
        {
            trava.Open();
            using (var pragma = trava.CreateCommand())
            {
                pragma.CommandText = "PRAGMA locking_mode=EXCLUSIVE";
                pragma.ExecuteNonQuery();
            }
            using (var inicio = trava.CreateCommand())
            {
                inicio.CommandText = "BEGIN IMMEDIATE";
                inicio.ExecuteNonQuery();
            }
            using (var escrita = trava.CreateCommand())
            {
                escrita.CommandText = "CREATE TABLE trava_teste (x INTEGER)";
                escrita.ExecuteNonQuery();
            }

            var resultado = servico.RestaurarPara(caminhoBackup);

            Assert.False(resultado.Sucesso);
            Assert.Empty(Directory.GetFiles(_paths.BackupsDirectory, "corrompido-*"));
            Assert.Single(Directory.GetFiles(_paths.BackupsDirectory, "varthex-comanda-*.db"));

            using var desfazer = trava.CreateCommand();
            desfazer.CommandText = "ROLLBACK";
            desfazer.ExecuteNonQuery();
        }

        SqliteConnection.ClearAllPools();
        Assert.Equal(hashAntes, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(_paths.DatabasePath)));
    }

    [Fact]
    public void RestaurarPara_ComBancoAtivoSaudavel_ContinuaUsandoBackupGerenciado()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var backupValido = servico.CriarBackupGerenciado();
        var caminhoBackup = Path.Combine(backupValido.Valor!.Destino, backupValido.Valor.Arquivo);
        relogio.UtcNow = relogio.UtcNow.AddSeconds(5);

        var resultado = servico.RestaurarPara(caminhoBackup);

        Assert.True(resultado.Sucesso);
        Assert.Empty(Directory.GetFiles(_paths.BackupsDirectory, "corrompido-*"));
        Assert.StartsWith("varthex-comanda-", resultado.Valor!.Arquivo);
        Assert.Equal(2, Directory.GetFiles(_paths.BackupsDirectory, "varthex-comanda-*.db").Length);
    }

    [Fact]
    public void RestaurarPara_ArquivoCorrompidoComoOrigem_ContinuaFalhandoESemAlterarABaseAtiva()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        using (var contexto = _fabrica.CreateDbContext())
        {
            contexto.Categorias.Add(new Categoria { Id = 0, Nome = "Preservada", Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
            contexto.SaveChanges();
        }
        var origemCorrompida = Path.Combine(_raizTeste, "corrompido.db");
        File.WriteAllText(origemCorrompida, new string('x', 5000));

        var resultado = servico.RestaurarPara(origemCorrompida);

        Assert.False(resultado.Sucesso);
        Assert.Empty(Directory.GetFiles(_paths.BackupsDirectory, "corrompido-*"));
        SqliteConnection.ClearAllPools();
        using (var contexto = _fabrica.CreateDbContext())
        {
            Assert.Contains("Preservada", contexto.Categorias.Select(c => c.Nome).ToList());
        }
    }

    // RNF16: falha tecnica real de I/O e registrada; falha esperada (arquivo inexistente) nao.
    [Fact]
    public void CriarBackupExterno_FalhaDeIO_RegistraErroNoLogEDevolveFalha()
    {
        var coletor = new ColetorDeLog();
        var servico = new EfBackupService(_paths, _registros, new FakeClockDeIntegracao(), logger: coletor.Logger);
        var arquivoNoLugarDaPasta = Path.Combine(_raizTeste, "nao-e-pasta");
        File.WriteAllText(arquivoNoLugarDaPasta, "x");

        var resultado = servico.CriarBackupExterno(arquivoNoLugarDaPasta);

        Assert.False(resultado.Sucesso);
        var evento = Assert.Single(coletor.Eventos);
        Assert.Equal(LogEventLevel.Error, evento.Level);
        Assert.NotNull(evento.Exception);
        Assert.Equal("CriarBackup", ((ScalarValue)evento.Properties["Operacao"]).Value);
    }

    [Fact]
    public void RestaurarPara_ArquivoInexistente_NaoRegistraNoLog()
    {
        var coletor = new ColetorDeLog();
        var servico = new EfBackupService(_paths, _registros, new FakeClockDeIntegracao(), logger: coletor.Logger);

        var resultado = servico.RestaurarPara(Path.Combine(_raizTeste, "nao-existe.db"));

        Assert.False(resultado.Sucesso);
        Assert.Empty(coletor.Eventos);
    }

    [Fact]
    public void RestaurarPara_ArquivoCorrompido_RegistraErroNoLog()
    {
        var coletor = new ColetorDeLog();
        var servico = new EfBackupService(_paths, _registros, new FakeClockDeIntegracao(), logger: coletor.Logger);
        var caminhoInvalido = Path.Combine(_raizTeste, "invalido.db");
        File.WriteAllText(caminhoInvalido, "nao e um banco");

        var resultado = servico.RestaurarPara(caminhoInvalido);

        Assert.False(resultado.Sucesso);
        var evento = Assert.Single(coletor.Eventos);
        Assert.Equal(LogEventLevel.Error, evento.Level);
        Assert.NotNull(evento.Exception);
        Assert.Equal("RestaurarBackup", ((ScalarValue)evento.Properties["Operacao"]).Value);
    }

    private sealed class RegistrosQueFalhamAoRegistrar : IBackupRegistroRepository
    {
        public void Registrar(BackupRegistro registro) => throw new InvalidOperationException("registro indisponivel");

        public IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade) => [];

        public bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc) => false;
    }

    // A falha ao registrar (que vive no banco) depois do arquivo no lugar nao invalida o backup.
    [Fact]
    public void CriarBackupGerenciado_FalhaAoRegistrarDepoisDeMoverOArquivo_ContinuaSucessoELogaAviso()
    {
        var coletor = new ColetorDeLog();
        var servico = new EfBackupService(_paths, new RegistrosQueFalhamAoRegistrar(), new FakeClockDeIntegracao(), logger: coletor.Logger);

        var resultado = servico.CriarBackupGerenciado();

        Assert.True(resultado.Sucesso);
        var caminho = Path.Combine(resultado.Valor!.Destino, resultado.Valor.Arquivo);
        Assert.True(File.Exists(caminho));
        Assert.True(File.Exists(caminho + ".sha256"));
        Assert.Equal(StatusBackup.Sucesso, resultado.Valor.Status);
        var evento = Assert.Single(coletor.Eventos);
        Assert.Equal(LogEventLevel.Warning, evento.Level);
        Assert.Equal("RegistrarBackup", ((ScalarValue)evento.Properties["Operacao"]).Value);
    }

    [Fact]
    public void CriarBackupGerenciado_FalhaAoRegistrar_RetencaoAindaRemoveOsMaisAntigos()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, new RegistrosQueFalhamAoRegistrar(), relogio, retencaoMaxima: 2);

        for (var i = 0; i < 4; i++)
        {
            relogio.UtcNow = relogio.UtcNow.AddSeconds(1);
            Assert.True(servico.CriarBackupGerenciado().Sucesso);
        }

        Assert.Equal(2, Directory.GetFiles(_paths.BackupsDirectory, "varthex-comanda-*.db").Length);
    }

    // Arquivos auxiliares do banco antigo nao podem ser aplicados ao banco restaurado.
    [Fact]
    public void RestaurarPara_RemoveArquivosWalShmEJournalDoBancoAntigo()
    {
        var relogio = new FakeClockDeIntegracao();
        var servico = new EfBackupService(_paths, _registros, relogio);
        var backupValido = servico.CriarBackupGerenciado();
        var caminhoBackup = Path.Combine(backupValido.Valor!.Destino, backupValido.Valor.Arquivo);
        SqliteConnection.ClearAllPools();
        var auxiliares = new[] { "-wal", "-shm", "-journal" }.Select(s => _paths.DatabasePath + s).ToArray();
        foreach (var auxiliar in auxiliares)
        {
            File.WriteAllText(auxiliar, "lixo do banco antigo");
        }
        relogio.UtcNow = relogio.UtcNow.AddSeconds(5);

        var resultado = servico.RestaurarPara(caminhoBackup);

        Assert.True(resultado.Sucesso);
        Assert.All(auxiliares, auxiliar => Assert.False(File.Exists(auxiliar), auxiliar));
        using var conexao = new SqliteConnection($"Data Source={_paths.DatabasePath};Pooling=False");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "PRAGMA integrity_check";
        Assert.Equal("ok", (string?)comando.ExecuteScalar());
    }

    private class FakeClockDeIntegracao : VarthexComanda.Application.Abstractions.IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);
    }

    private class FakeBackupRegistroRepositoryDeIntegracao : IBackupRegistroRepository
    {
        private readonly List<BackupRegistro> _registros = new();
        private int _proximoId = 1;

        public void Registrar(BackupRegistro registro)
        {
            registro.Id = _proximoId++;
            _registros.Add(registro);
        }

        public IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade) =>
            _registros.OrderByDescending(r => r.CriadoEm).Take(quantidade).ToList();

        public bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc) =>
            _registros.Any(r => r.Status == StatusBackup.Sucesso && r.CriadoEm >= inicioUtc && r.CriadoEm < fimUtc);
    }
}
