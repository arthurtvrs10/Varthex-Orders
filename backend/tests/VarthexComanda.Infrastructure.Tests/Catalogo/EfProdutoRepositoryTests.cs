using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Catalogo;

public class EfProdutoRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;
    private readonly int _categoriaId;

    public EfProdutoRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-comanda-tests-{Guid.NewGuid()}.db");

        var servicos = new ServiceCollection();
        servicos.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={_dbPath};Foreign Keys=True"));
        _provedor = servicos.BuildServiceProvider();
        _fabrica = _provedor.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();

        using var contexto = _fabrica.CreateDbContext();
        contexto.Database.Migrate();
        var agora = DateTime.UtcNow;
        var categoria = new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora };
        contexto.Categorias.Add(categoria);
        contexto.SaveChanges();
        _categoriaId = categoria.Id;
    }

    public void Dispose()
    {
        _provedor.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void Salvar_ProdutoNovo_AtribuiIdEPersiste()
    {
        var repositorio = new EfProdutoRepository(_fabrica);
        var agora = DateTime.UtcNow;

        var salvo = repositorio.Salvar(new Produto
        {
            Id = 0, CategoriaId = _categoriaId, Nome = "Refrigerante", PrecoCentavos = 500,
            Ativo = true, CriadoEm = agora, AtualizadoEm = agora
        });

        Assert.True(salvo.Id > 0);
        var carregado = repositorio.BuscarPorId(salvo.Id);
        Assert.NotNull(carregado);
        Assert.Equal("Refrigerante", carregado!.Nome);
    }

    [Fact]
    public void Pesquisar_FiltraPorCategoriaEPorTextoSemDiferenciarMaiusculas()
    {
        var repositorio = new EfProdutoRepository(_fabrica);
        var agora = DateTime.UtcNow;
        repositorio.Salvar(new Produto { Id = 0, CategoriaId = _categoriaId, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        repositorio.Salvar(new Produto { Id = 0, CategoriaId = _categoriaId, Nome = "Suco Natural", PrecoCentavos = 700, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });

        var porTexto = repositorio.Pesquisar(null, "REFRI");
        Assert.Single(porTexto);
        Assert.Equal("Refrigerante", porTexto[0].Nome);

        var porCategoria = repositorio.Pesquisar(_categoriaId, null);
        Assert.Equal(2, porCategoria.Count);
    }

    [Fact]
    public void Salvar_ProdutoExistente_AtualizaSemCriarNovoRegistro()
    {
        var repositorio = new EfProdutoRepository(_fabrica);
        var agora = DateTime.UtcNow;
        var produto = repositorio.Salvar(new Produto { Id = 0, CategoriaId = _categoriaId, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });

        produto.PrecoCentavos = 600;
        repositorio.Salvar(produto);

        var carregado = repositorio.BuscarPorId(produto.Id);
        Assert.Equal(600, carregado!.PrecoCentavos);
        Assert.Single(repositorio.Pesquisar(null, null));
    }

    [Fact]
    public void Salvar_ProdutoComFoto_PersisteNomeDoArquivo()
    {
        var repositorio = new EfProdutoRepository(_fabrica);

        var salvo = repositorio.Salvar(new Produto
        {
            Id = 0,
            CategoriaId = _categoriaId,
            Nome = "X-Burguer",
            PrecoCentavos = 1800,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow,
            FotoArquivo = "abc123.jpg"
        });

        var lido = repositorio.BuscarPorId(salvo.Id);
        Assert.Equal("abc123.jpg", lido!.FotoArquivo);
    }

    [Fact]
    public void Salvar_ProdutoSemFoto_FotoArquivoFicaNula()
    {
        var repositorio = new EfProdutoRepository(_fabrica);

        var salvo = repositorio.Salvar(new Produto
        {
            Id = 0,
            CategoriaId = _categoriaId,
            Nome = "Coca-Cola",
            PrecoCentavos = 500,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow
        });

        Assert.Null(repositorio.BuscarPorId(salvo.Id)!.FotoArquivo);
    }

    [Fact]
    public void Salvar_LimparFotoDeProdutoExistente_PersisteNulo()
    {
        var repositorio = new EfProdutoRepository(_fabrica);
        var salvo = repositorio.Salvar(new Produto
        {
            Id = 0,
            CategoriaId = _categoriaId,
            Nome = "Suco",
            PrecoCentavos = 800,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            AtualizadoEm = DateTime.UtcNow,
            FotoArquivo = "velha.png"
        });

        var carregado = repositorio.BuscarPorId(salvo.Id)!;
        carregado.FotoArquivo = null;
        repositorio.Salvar(carregado);

        Assert.Null(repositorio.BuscarPorId(salvo.Id)!.FotoArquivo);
    }

    [Fact]
    public void ListarNomesDeFotos_DevolveSoOsNomesPreenchidos()
    {
        var repositorio = new EfProdutoRepository(_fabrica);
        var agora = DateTime.UtcNow;
        foreach (var (nome, foto) in new (string, string?)[] { ("A", "a.png"), ("B", null), ("C", ""), ("D", "d.jpg") })
        {
            repositorio.Salvar(new Produto
            {
                Id = 0, CategoriaId = _categoriaId, Nome = nome, PrecoCentavos = 100,
                Ativo = true, CriadoEm = agora, AtualizadoEm = agora, FotoArquivo = foto
            });
        }

        var nomes = repositorio.ListarNomesDeFotos();

        Assert.Equal(new[] { "a.png", "d.jpg" }, nomes.OrderBy(n => n).ToArray());
    }
}
