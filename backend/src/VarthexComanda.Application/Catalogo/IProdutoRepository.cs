using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public interface IProdutoRepository
{
    Produto? BuscarPorId(int id);
    IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto);
    Produto Salvar(Produto produto);

    /// <summary>Somente leitura: nomes de arquivo de foto referenciados por algum produto (sem nulos/vazios).</summary>
    IReadOnlyList<string> ListarNomesDeFotos();
}
