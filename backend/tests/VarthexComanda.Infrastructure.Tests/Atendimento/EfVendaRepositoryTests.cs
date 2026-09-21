using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Atendimento;

public class EfVendaRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;

    public EfVendaRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-venda-tests-{Guid.NewGuid()}.db");

        var servicos = new ServiceCollection();
        servicos.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={_dbPath};Foreign Keys=True"));
        _provedor = servicos.BuildServiceProvider();
        _fabrica = _provedor.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();

        using var contexto = _fabrica.CreateDbContext();
        contexto.Database.Migrate();
    }

    public void Dispose()
    {
        _provedor.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private Venda CriarVendaConcluida(int numeroComanda, DateTime finalizadaEmUtc)
    {
        var comandas = new EfComandaRepository(_fabrica);
        var categorias = new EfCategoriaRepository(_fabrica);
        var produtos = new EfProdutoRepository(_fabrica);
        var agora = finalizadaEmUtc.AddMinutes(-5);

        var categoria = categorias.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        var produto = produtos.Salvar(new Produto { Id = 0, CategoriaId = categoria.Id, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        var comanda = comandas.AbrirComanda(numeroComanda, agora);
        comandas.AdicionarItem(comanda.Id, produto, 2, agora);

        return comandas.EncerrarComanda(comanda.Id, finalizadaEmUtc);
    }

    [Fact]
    [Trait("Caso", "CT12")]
    public void ListarPorData_VendaDentroDoIntervalo_RetornaComNumeroDaComanda()
    {
        var venda = CriarVendaConcluida(10, new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc));
        var repositorio = new EfVendaRepository(_fabrica);

        var resultado = repositorio.ListarPorData(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.Single(resultado);
        Assert.Equal(venda.Id, resultado[0].Venda.Id);
        Assert.Equal(10, resultado[0].NumeroComanda);
        Assert.Equal(1000, resultado[0].Venda.TotalCentavos);
    }

    [Fact]
    public void ListarPorData_VendaForaDoIntervalo_NaoRetorna()
    {
        CriarVendaConcluida(10, new DateTime(2026, 9, 17, 23, 59, 0, DateTimeKind.Utc));
        var repositorio = new EfVendaRepository(_fabrica);

        var resultado = repositorio.ListarPorData(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.Empty(resultado);
    }

    [Fact]
    public void ListarPorData_VendaExatamenteNoLimiteInicial_Retorna()
    {
        CriarVendaConcluida(10, new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc));
        var repositorio = new EfVendaRepository(_fabrica);

        var resultado = repositorio.ListarPorData(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.Single(resultado);
    }

    [Fact]
    public void ListarPorData_VendaExatamenteNoLimiteFinal_NaoRetorna()
    {
        CriarVendaConcluida(10, new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));
        var repositorio = new EfVendaRepository(_fabrica);

        var resultado = repositorio.ListarPorData(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.Empty(resultado);
    }

    [Fact]
    public void ListarPorData_SemVendas_RetornaListaVazia()
    {
        var repositorio = new EfVendaRepository(_fabrica);

        var resultado = repositorio.ListarPorData(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.Empty(resultado);
    }

    [Fact]
    [Trait("Caso", "CT12")]
    public void BuscarItensDaVenda_VendaExistente_RetornaItensDaComandaOriginal()
    {
        var venda = CriarVendaConcluida(10, new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc));
        var repositorio = new EfVendaRepository(_fabrica);

        var itens = repositorio.BuscarItensDaVenda(venda.Id);

        Assert.NotNull(itens);
        Assert.Single(itens!);
        Assert.Equal(2, itens![0].Quantidade);
        Assert.Equal(1000, itens[0].SubtotalCentavos);
    }

    [Fact]
    public void BuscarItensDaVenda_VendaInexistente_RetornaNull()
    {
        var repositorio = new EfVendaRepository(_fabrica);

        var itens = repositorio.BuscarItensDaVenda(999);

        Assert.Null(itens);
    }
}
