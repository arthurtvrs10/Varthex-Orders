using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using VarthexComanda.Infrastructure.Storage;

namespace VarthexComanda.Infrastructure.Tests.Homologacao;

/// <summary>
/// Banco SQLite real e descartavel (pasta temporaria propria) usado pelos testes de homologacao.
/// Nunca toca em %LOCALAPPDATA%\VarthexComanda: a raiz e sempre uma pasta nova em Path.GetTempPath().
/// E publico porque o Desktop.Tests tambem o reutiliza (CT09).
/// </summary>
public sealed class BancoDeHomologacao : IDisposable
{
    private readonly ServiceProvider _provedor;

    public BancoDeHomologacao(bool migrar = true, params IInterceptor[] interceptadores)
    {
        Raiz = Path.Combine(Path.GetTempPath(), $"varthex-homologacao-{Guid.NewGuid()}");
        Paths = new AppPaths(Raiz);
        Paths.EnsureCreated();

        var servicos = new ServiceCollection();
        servicos.AddDbContextFactory<VarthexComandaDbContext>(opcoes =>
        {
            opcoes.UseSqlite($"Data Source={Paths.DatabasePath};Foreign Keys=True");
            if (interceptadores.Length > 0) opcoes.AddInterceptors(interceptadores);
        });
        _provedor = servicos.BuildServiceProvider();
        Fabrica = _provedor.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();

        if (migrar)
        {
            using var contexto = Fabrica.CreateDbContext();
            contexto.Database.Migrate();
        }
    }

    public string Raiz { get; }
    public AppPaths Paths { get; }
    public string CaminhoDb => Paths.DatabasePath;
    public IDbContextFactory<VarthexComandaDbContext> Fabrica { get; }

    /// <summary>Insere categoria + produto ativos pelo repositorio real.</summary>
    public Produto CriarProduto(string nome, long precoCentavos, string categoria = "Geral")
    {
        var agora = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);
        var repositorioCategorias = new EfCategoriaRepository(Fabrica);
        var existente = repositorioCategorias.ListarAtivas().FirstOrDefault(c => c.Nome == categoria);
        var cat = existente ?? repositorioCategorias.Salvar(
            new Categoria { Id = 0, Nome = categoria, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        return new EfProdutoRepository(Fabrica).Salvar(new Produto
        {
            Id = 0, CategoriaId = cat.Id, Nome = nome, PrecoCentavos = precoCentavos,
            Ativo = true, CriadoEm = agora, AtualizadoEm = agora
        });
    }

    public void Dispose()
    {
        _provedor.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(Raiz, recursive: true); } catch { /* limpeza best-effort */ }
    }
}

public sealed class RelogioFixo : IClock
{
    public RelogioFixo(DateTime? agora = null) =>
        UtcNow = agora ?? new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow { get; set; }
}
