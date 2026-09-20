using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VarthexComanda.Infrastructure.Persistence;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

// Instalacao nova nao tem dados a preservar: sem backup preventivo antes de migrar.
public class MigracaoDoBancoTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"varthex-migracao-tests-{Guid.NewGuid()}");

    public MigracaoDoBancoTests() => Directory.CreateDirectory(_raiz);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_raiz, recursive: true); } catch { }
    }

    private VarthexComandaDbContext CriarContexto()
    {
        var opcoes = new DbContextOptionsBuilder<VarthexComandaDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_raiz, "banco.db")};Pooling=False")
            .Options;
        return new VarthexComandaDbContext(opcoes);
    }

    [Fact]
    public void BancoNovo_ComMigracoesPendentes_NaoExigeBackupPreventivo()
    {
        using var contexto = CriarContexto();

        var exige = MigracaoDoBanco.ExigeBackupPreventivo(contexto.Database, out var pendentes);

        Assert.False(exige);
        Assert.True(pendentes > 0);
    }

    [Fact]
    public void BancoComAlgumasMigracoesAplicadasEOutrasPendentes_ExigeBackupPreventivo()
    {
        using var contexto = CriarContexto();
        var todas = contexto.Database.GetMigrations().ToList();
        Assert.True(todas.Count >= 2, "o teste precisa de pelo menos duas migracoes");
        contexto.GetService<IMigrator>().Migrate(todas[0]);

        var exige = MigracaoDoBanco.ExigeBackupPreventivo(contexto.Database, out var pendentes);

        Assert.True(exige);
        Assert.Equal(todas.Count - 1, pendentes);
    }

    [Fact]
    public void BancoTotalmenteMigrado_NaoExigeBackupPreventivo()
    {
        using var contexto = CriarContexto();
        contexto.Database.Migrate();

        var exige = MigracaoDoBanco.ExigeBackupPreventivo(contexto.Database, out var pendentes);

        Assert.False(exige);
        Assert.Equal(0, pendentes);
    }
}
