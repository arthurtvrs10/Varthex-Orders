using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Atendimento;

// RF27, RN22, RN23, RNF20, CT20
// Cada "fase" abre repositorios novos sobre o mesmo arquivo SQLite real, simulando
// o fechamento e a reabertura do aplicativo.
public class RecuperacaoDeAtendimentoTests : IDisposable
{
    private readonly string _pasta;
    private readonly string _dbPath;
    private readonly List<ServiceProvider> _provedores = new();

    public RecuperacaoDeAtendimentoTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), $"varthex-recuperacao-{Guid.NewGuid()}");
        Directory.CreateDirectory(_pasta);
        _dbPath = Path.Combine(_pasta, "varthex-comanda.db");
    }

    public void Dispose()
    {
        EncerrarFase();
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, recursive: true);
    }

    private IDbContextFactory<VarthexComandaDbContext> IniciarFase()
    {
        var servicos = new ServiceCollection();
        servicos.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={_dbPath};Foreign Keys=True"));
        var provedor = servicos.BuildServiceProvider();
        _provedores.Add(provedor);
        var fabrica = provedor.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();
        using var contexto = fabrica.CreateDbContext();
        contexto.Database.Migrate();
        return fabrica;
    }

    private void EncerrarFase()
    {
        foreach (var provedor in _provedores) provedor.Dispose();
        _provedores.Clear();
        SqliteConnection.ClearAllPools();
    }

    private static Produto CriarProduto(IDbContextFactory<VarthexComandaDbContext> fabrica, string nome, long preco)
    {
        var agora = DateTime.UtcNow;
        var categoria = new EfCategoriaRepository(fabrica).Salvar(
            new Categoria { Id = 0, Nome = $"Cat {nome}", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        return new EfProdutoRepository(fabrica).Salvar(
            new Produto { Id = 0, CategoriaId = categoria.Id, Nome = nome, PrecoCentavos = preco, Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
    }

    private record ItemVisto(string Nome, int Quantidade, long Preco, long Subtotal);

    private record ComandaVista(long Total, List<ItemVisto> Itens);

    private static ComandaVista Ler(EfComandaRepository repo, int comandaId)
    {
        var detalhe = repo.BuscarComItens(comandaId)!;
        var itens = detalhe.Itens
            .OrderBy(i => i.Id)
            .Select(i => new ItemVisto(i.NomeProduto, i.Quantidade, i.PrecoUnitarioCentavos, i.SubtotalCentavos))
            .ToList();
        return new ComandaVista(detalhe.Comanda.TotalCentavos, itens);
    }

    private static Dictionary<int, StatusComanda> Status(IDbContextFactory<VarthexComandaDbContext> fabrica)
    {
        using var ctx = fabrica.CreateDbContext();
        return ctx.Comandas.AsNoTracking().ToDictionary(c => c.Id, c => c.Status);
    }

    [Fact]
    [Trait("Requisito", "RF27")]
    [Trait("Requisito", "RNF20")]
    [Trait("Caso", "CT20")]
    public void ComandasAbertas_ReaparecemAposReinicio_ComMesmosItensETotais()
    {
        var fabrica = IniciarFase();
        var repo = new EfComandaRepository(fabrica);
        var refri = CriarProduto(fabrica, "Refrigerante", 500);
        var suco = CriarProduto(fabrica, "Suco", 750);
        var agora = DateTime.UtcNow;

        var umItem = repo.AbrirComanda(1, agora);
        repo.AdicionarItem(umItem.Id, refri, 1, agora);
        var varios = repo.AbrirComanda(2, agora);
        repo.AdicionarItem(varios.Id, refri, 2, agora);
        repo.AdicionarItem(varios.Id, suco, 3, agora);
        repo.AdicionarItem(varios.Id, refri, 1, agora);
        var vazia = repo.AbrirComanda(3, agora);

        var ids = new[] { umItem.Id, varios.Id, vazia.Id };
        var antes = ids.ToDictionary(id => id, id => Ler(repo, id));
        var abertasAntes = repo.ListarAbertas().Select(c => (c.Id, c.Numero, c.Status)).ToList();

        EncerrarFase();
        var repoNovo = new EfComandaRepository(IniciarFase());

        var abertasDepois = repoNovo.ListarAbertas().Select(c => (c.Id, c.Numero, c.Status)).ToList();
        Assert.Equal(3, abertasDepois.Count);
        Assert.Equal(abertasAntes, abertasDepois);
        Assert.All(abertasDepois, c => Assert.Equal(StatusComanda.Aberta, c.Status));

        foreach (var (id, esperado) in antes)
        {
            var depois = Ler(repoNovo, id);
            Assert.Equal(esperado.Total, depois.Total);
            Assert.Equal(esperado.Itens, depois.Itens);
        }
        Assert.Empty(antes[vazia.Id].Itens);
        Assert.Equal(0, antes[vazia.Id].Total);
        Assert.Equal(500 * 3 + 750 * 3, antes[varios.Id].Total);
        Assert.Equal(antes[varios.Id].Total, antes[varios.Id].Itens.Sum(i => i.Subtotal));
    }

    [Fact]
    [Trait("Requisito", "RN22")]
    [Trait("Requisito", "RF27")]
    [Trait("Caso", "CT20")]
    public void Reinicio_NaoCriaVendaNemAlteraStatus()
    {
        var fabrica = IniciarFase();
        var repo = new EfComandaRepository(fabrica);
        var produto = CriarProduto(fabrica, "Refrigerante", 500);
        var agora = DateTime.UtcNow;
        var a = repo.AbrirComanda(1, agora);
        repo.AdicionarItem(a.Id, produto, 2, agora);
        repo.AbrirComanda(2, agora);

        using (var ctx = fabrica.CreateDbContext())
        {
            Assert.Equal(0, ctx.Vendas.Count());
        }
        var statusAntes = Status(fabrica);

        EncerrarFase();
        var fabricaNova = IniciarFase();

        using var contexto = fabricaNova.CreateDbContext();
        Assert.Equal(0, contexto.Vendas.Count());
        Assert.Equal(statusAntes, Status(fabricaNova));
        Assert.Equal(2, statusAntes.Count);
        Assert.All(statusAntes.Values, s => Assert.Equal(StatusComanda.Aberta, s));
    }

    [Fact]
    [Trait("Requisito", "RN23")]
    [Trait("Requisito", "RNF20")]
    [Trait("Caso", "CT20")]
    public void Reinicio_NaoDuplicaNemDescartaItemConfirmado()
    {
        var fabrica = IniciarFase();
        var repo = new EfComandaRepository(fabrica);
        var refri = CriarProduto(fabrica, "Refrigerante", 500);
        var suco = CriarProduto(fabrica, "Suco", 750);
        var agora = DateTime.UtcNow;
        var comanda = repo.AbrirComanda(1, agora);
        repo.AdicionarItem(comanda.Id, refri, 2, agora);
        var detalhe = repo.AdicionarItem(comanda.Id, suco, 1, agora);
        var itemSuco = detalhe.Itens.Single(i => i.NomeProduto == "Suco");

        using (var ctx = fabrica.CreateDbContext())
        {
            Assert.Equal(2, ctx.ItensComanda.Count());
        }

        repo.AlterarQuantidade(itemSuco.Id, 4, agora);
        EncerrarFase();

        var fabricaNova = IniciarFase();
        using (var ctx = fabricaNova.CreateDbContext())
        {
            Assert.Equal(2, ctx.ItensComanda.Count());
        }
        var depois = new EfComandaRepository(fabricaNova).BuscarComItens(comanda.Id)!;
        var sucoDepois = depois.Itens.Single(i => i.NomeProduto == "Suco");
        Assert.Equal(4, sucoDepois.Quantidade);
        Assert.Equal(3000, sucoDepois.SubtotalCentavos);
        Assert.Equal(2, depois.Itens.Single(i => i.NomeProduto == "Refrigerante").Quantidade);
        Assert.Equal(1000 + 3000, depois.Comanda.TotalCentavos);
    }

    [Fact]
    [Trait("Requisito", "RF27")]
    [Trait("Requisito", "RN22")]
    [Trait("Caso", "CT20")]
    public void ComandaEncerrada_NaoReaparecemComoAberta()
    {
        var fabrica = IniciarFase();
        var repo = new EfComandaRepository(fabrica);
        var produto = CriarProduto(fabrica, "Refrigerante", 500);
        var agora = DateTime.UtcNow;
        var encerrada = repo.AbrirComanda(1, agora);
        repo.AdicionarItem(encerrada.Id, produto, 2, agora);
        var venda = repo.EncerrarComanda(encerrada.Id, agora);
        var aberta = repo.AbrirComanda(2, agora);

        EncerrarFase();
        var fabricaNova = IniciarFase();
        var repoNovo = new EfComandaRepository(fabricaNova);

        var abertas = repoNovo.ListarAbertas();
        Assert.Equal(new[] { aberta.Id }, abertas.Select(c => c.Id).ToArray());
        Assert.DoesNotContain(abertas, c => c.Id == encerrada.Id);

        using var ctx = fabricaNova.CreateDbContext();
        var vendas = ctx.Vendas.Where(v => v.ComandaId == encerrada.Id).ToList();
        Assert.Single(vendas);
        Assert.Equal(venda.Id, vendas[0].Id);
        Assert.Equal(1000, vendas[0].TotalCentavos);
    }
}
