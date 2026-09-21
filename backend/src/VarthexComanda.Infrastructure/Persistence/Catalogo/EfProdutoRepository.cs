using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Catalogo;

public class EfProdutoRepository : IProdutoRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfProdutoRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public Produto? BuscarPorId(int id)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Produtos.SingleOrDefault(p => p.Id == id);
    }

    public IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var consulta = contexto.Produtos.AsQueryable();

        if (categoriaId is not null)
        {
            consulta = consulta.Where(p => p.CategoriaId == categoriaId);
        }

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var textoBusca = texto.Trim().ToLower();
            consulta = consulta.Where(p => p.Nome.ToLower().Contains(textoBusca));
        }

        return consulta.OrderBy(p => p.Nome).ToList();
    }

    public IReadOnlyList<string> ListarNomesDeFotos()
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Produtos
            .AsNoTracking()
            .Where(p => p.FotoArquivo != null && p.FotoArquivo != "")
            .Select(p => p.FotoArquivo!)
            .ToList();
    }

    public Produto Salvar(Produto produto)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        if (produto.Id == 0)
        {
            contexto.Produtos.Add(produto);
        }
        else
        {
            contexto.Produtos.Update(produto);
        }
        contexto.SaveChanges();
        return produto;
    }
}
