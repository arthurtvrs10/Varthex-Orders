using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Desktop;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Storage;

namespace VarthexComanda.Desktop.Tests;

public sealed class DemonstracaoTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "comanda-demo-test-" + Guid.NewGuid());
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly VarthexComandaDbContext _db;
    private static string Origem => Path.Combine(AppContext.BaseDirectory, "demo");

    public DemonstracaoTests()
    {
        _connection.Open();
        _db = new(new DbContextOptionsBuilder<VarthexComandaDbContext>().UseSqlite(_connection).Options);
        _db.Database.Migrate();
    }

    [Fact]
    public void CatalogoReal_CarregaDozeProdutosComFotosValidasESemVendas()
    {
        var paths = new AppPaths(_root);
        Assert.True(CatalogoDemonstracao.Inicializar(_db, paths, Origem));
        Assert.Equal(4, _db.Categorias.Count());
        Assert.Equal(12, _db.Produtos.Count());
        Assert.Empty(_db.Comandas);
        Assert.Empty(_db.Vendas);
        foreach (var produto in _db.Produtos)
        {
            Assert.True(produto.Ativo);
            Assert.True(produto.PrecoCentavos > 0);
            using var foto = File.OpenRead(Path.Combine(paths.FotosDirectory, produto.FotoArquivo!));
            var frame = BitmapFrame.Create(foto, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            Assert.True(frame.PixelWidth >= 512 && frame.PixelHeight >= 512);
        }
    }

    [Fact]
    public void Reabrir_PreservaPrecoEditadoEProdutoDesativado()
    {
        var paths = new AppPaths(_root);
        CatalogoDemonstracao.Inicializar(_db, paths, Origem);
        var produto = _db.Produtos.First();
        produto.PrecoCentavos = 1999;
        produto.Ativo = false;
        _db.SaveChanges();
        Assert.False(CatalogoDemonstracao.Inicializar(_db, paths, Origem));
        _db.ChangeTracker.Clear();
        var salvo = _db.Produtos.Single(p => p.Id == produto.Id);
        Assert.Equal(1999, salvo.PrecoCentavos);
        Assert.False(salvo.Ativo);
        Assert.Equal(12, _db.Produtos.Count());
    }

    [Fact]
    public void BaseExistente_SemMarcador_NaoRecebeExemplos()
    {
        _db.Categorias.Add(new Categoria { Id = 0, Nome = "Meu catálogo", Ativo = true,
            CriadoEm = DateTime.UtcNow, AtualizadoEm = DateTime.UtcNow });
        _db.SaveChanges();
        Assert.False(CatalogoDemonstracao.Inicializar(_db, new AppPaths(_root), "pasta inexistente"));
        Assert.Single(_db.Categorias);
        Assert.Empty(_db.Produtos);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void FotoAusente_NaoGravaCatalogoParcial()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "catalogo.json"), JsonSerializer.Serialize(new[] {
            new CatalogoDemonstracao.Item("Pastéis", "Pastel", 1000, "ausente.png") }));
        Assert.Throws<FileNotFoundException>(() => CatalogoDemonstracao.Inicializar(_db, new AppPaths(_root), _root));
        Assert.Empty(_db.Categorias);
        Assert.Empty(_db.Produtos);
        Assert.False(_db.Configuracoes.Any(x => x.Chave == CatalogoDemonstracao.Marcador));
    }

    [Theory]
    [InlineData("../foto.png")]
    [InlineData("C:\\foto.png")]
    public void FotoForaDoCatalogo_ERecusada(string foto)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "catalogo.json"), JsonSerializer.Serialize(new[] {
            new CatalogoDemonstracao.Item("Pastéis", "Pastel", 1000, foto) }));
        Assert.Throws<InvalidDataException>(() => CatalogoDemonstracao.Inicializar(_db, new AppPaths(_root), _root));
        Assert.Empty(_db.Produtos);
    }

    [Fact]
    public void Demonstracao_IgnoraPastaDeProducaoENaoDependeDoAtalho()
    {
        Assert.Equal(Path.Combine(_root, "VarthexComandaDemo"), ModoExecucao.RaizDados(true, "dados-reais", _root));
        Assert.Equal("dados-reais", ModoExecucao.RaizDados(false, "dados-reais", _root));
        Assert.Null(ModoExecucao.RaizDados(false, null, _root));
        Assert.False(ModoExecucao.Demonstracao([], _root));
        Assert.True(ModoExecucao.Demonstracao(["--demonstracao"], _root));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "edicao-demonstracao.txt"), "demo");
        Assert.True(ModoExecucao.Demonstracao([], _root));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
