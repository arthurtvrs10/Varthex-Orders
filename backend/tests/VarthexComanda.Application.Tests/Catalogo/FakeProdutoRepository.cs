using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Catalogo;

public class FakeProdutoRepository : IProdutoRepository
{
    private readonly List<Produto> _produtos = new();
    private int _proximoId = 1;

    public Produto? BuscarPorId(int id) => _produtos.FirstOrDefault(p => p.Id == id);

    public IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto)
    {
        var consulta = _produtos.AsEnumerable();
        if (categoriaId is not null)
        {
            consulta = consulta.Where(p => p.CategoriaId == categoriaId);
        }
        if (!string.IsNullOrWhiteSpace(texto))
        {
            consulta = consulta.Where(p => p.Nome.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }
        return consulta.ToList();
    }

    public IReadOnlyList<string> ListarNomesDeFotos() =>
        _produtos.Where(p => !string.IsNullOrEmpty(p.FotoArquivo)).Select(p => p.FotoArquivo!).ToList();

    public Produto Salvar(Produto produto)
    {
        if (produto.Id == 0)
        {
            produto.Id = _proximoId++;
            _produtos.Add(produto);
        }
        return produto;
    }
}
