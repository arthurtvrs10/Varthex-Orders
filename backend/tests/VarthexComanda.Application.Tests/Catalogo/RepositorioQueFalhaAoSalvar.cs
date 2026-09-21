using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Catalogo;

internal sealed class RepositorioQueFalhaAoSalvar : IProdutoRepository
{
    private readonly FakeProdutoRepository _interno;

    public RepositorioQueFalhaAoSalvar(FakeProdutoRepository interno) => _interno = interno;

    public Produto? BuscarPorId(int id) => _interno.BuscarPorId(id);

    public IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto) => _interno.Pesquisar(categoriaId, texto);

    public IReadOnlyList<string> ListarNomesDeFotos() => _interno.ListarNomesDeFotos();

    public Produto Salvar(Produto produto) => throw new InvalidOperationException("falha simulada");
}
