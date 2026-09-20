using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Infrastructure.Persistence;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

// Etapa 7, ruling 4: arquivo que nem e SQLite (ou esta corrompido) deve levar o app ao modo
// de restauracao; a deteccao vem do codigo do erro do SQLite (SQLITE_CORRUPT=11, SQLITE_NOTADB=26).
public class CorrupcaoDeBancoTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"varthex-corrupcao-tests-{Guid.NewGuid()}");

    public CorrupcaoDeBancoTests() => Directory.CreateDirectory(_raiz);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(26)]
    public void EhErroDeCorrupcao_CodigosCorruptENotADb_Verdadeiro(int codigo)
    {
        Assert.True(CorrupcaoDeBanco.EhErroDeCorrupcao(new SqliteException("erro", codigo)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(13)]
    public void EhErroDeCorrupcao_OutrosCodigos_Falso(int codigo)
    {
        Assert.False(CorrupcaoDeBanco.EhErroDeCorrupcao(new SqliteException("erro", codigo)));
    }

    [Fact]
    public void EhErroDeCorrupcao_ExcecaoQualquerOuNula_Falso()
    {
        Assert.False(CorrupcaoDeBanco.EhErroDeCorrupcao(new InvalidOperationException("x")));
        Assert.False(CorrupcaoDeBanco.EhErroDeCorrupcao(null));
    }

    [Fact]
    public void EhErroDeCorrupcao_ProcuraNaExcecaoInterna()
    {
        var envolvida = new InvalidOperationException("externa", new SqliteException("erro", 26));

        Assert.True(CorrupcaoDeBanco.EhErroDeCorrupcao(envolvida));
    }

    private VarthexComandaDbContext CriarContexto(string caminho)
    {
        var opcoes = new DbContextOptionsBuilder<VarthexComandaDbContext>()
            .UseSqlite($"Data Source={caminho};Pooling=False")
            .Options;
        return new VarthexComandaDbContext(opcoes);
    }

    [Fact]
    public void ArquivoComBytesDeLixo_PrimeiraLeituraDoEfLancaErroClassificadoComoCorrupcao()
    {
        var caminho = Path.Combine(_raiz, "lixo.db");
        File.WriteAllText(caminho, new string('x', 5000));
        using var contexto = CriarContexto(caminho);

        // mesma sequencia usada por App.OnStartup
        var ex = Record.Exception(() => contexto.Database
            .SqlQueryRaw<string>("PRAGMA integrity_check")
            .AsEnumerable()
            .ToList());

        Assert.NotNull(ex);
        Assert.True(CorrupcaoDeBanco.EhErroDeCorrupcao(ex), ex.ToString());
    }

    [Fact]
    public void ArquivoComBytesDeLixo_MigrateLancaErroClassificadoComoCorrupcao()
    {
        var caminho = Path.Combine(_raiz, "lixo.db");
        File.WriteAllText(caminho, new string('x', 5000));
        using var contexto = CriarContexto(caminho);

        var ex = Record.Exception(() =>
        {
            contexto.Database.GetPendingMigrations().ToList();
            contexto.Database.Migrate();
        });

        Assert.NotNull(ex);
        Assert.True(CorrupcaoDeBanco.EhErroDeCorrupcao(ex), ex.ToString());
    }
}
