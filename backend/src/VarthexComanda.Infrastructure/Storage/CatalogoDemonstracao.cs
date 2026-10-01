using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;

namespace VarthexComanda.Infrastructure.Storage;

/// <summary>RF03/RNF09: catálogo fictício exclusivo da edição de demonstração.</summary>
public static class CatalogoDemonstracao
{
    public sealed record Item(string Categoria, string Nome, long PrecoCentavos, string Foto);
    public const string Marcador = "demo.catalogo.v1";

    public static bool Inicializar(VarthexComandaDbContext db, AppPaths paths, string origem)
    {
        // Nunca adicionar exemplos a um catálogo existente, mesmo sem o marcador.
        if (db.Configuracoes.Any(x => x.Chave == Marcador) || db.Categorias.Any()
            || db.Produtos.Any() || db.Comandas.Any()) return false;

        var itens = JsonSerializer.Deserialize<Item[]>(File.ReadAllText(Path.Combine(origem, "catalogo.json")))
            ?? throw new InvalidDataException("Catálogo de demonstração inválido.");
        if (itens.Length == 0 || itens.Any(i => string.IsNullOrWhiteSpace(i.Nome)
            || string.IsNullOrWhiteSpace(i.Categoria) || i.PrecoCentavos <= 0
            || string.IsNullOrWhiteSpace(i.Foto) || i.Foto != Path.GetFileName(i.Foto)
            || i.Foto.Contains('/') || i.Foto.Contains('\\') || i.Foto.Contains(':')
            || !i.Foto.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Produto de demonstração inválido.");

        // Validar todas as fotos antes de qualquer gravação no banco.
        foreach (var item in itens)
            if (!File.Exists(Path.Combine(origem, "fotos", item.Foto)))
                throw new FileNotFoundException("Foto de demonstração ausente: " + item.Foto);

        paths.EnsureCreated();
        using var transacao = db.Database.BeginTransaction();
        var agora = DateTime.UtcNow;
        foreach (var grupo in itens.GroupBy(x => x.Categoria))
        {
            var categoria = new Categoria { Id = 0, Nome = grupo.Key, Ativo = true, CriadoEm = agora, AtualizadoEm = agora };
            db.Categorias.Add(categoria);
            db.SaveChanges();
            foreach (var item in grupo)
            {
                var nomeFoto = "demo-" + item.Foto;
                File.Copy(Path.Combine(origem, "fotos", item.Foto), Path.Combine(paths.FotosDirectory, nomeFoto), true);
                db.Produtos.Add(new Produto { Id = 0, CategoriaId = categoria.Id, Nome = item.Nome,
                    PrecoCentavos = item.PrecoCentavos, FotoArquivo = nomeFoto, Ativo = true,
                    CriadoEm = agora, AtualizadoEm = agora });
            }
        }
        db.Configuracoes.Add(new Domain.Configuracao { Chave = Marcador, Valor = "1", AtualizadoEm = agora });
        db.SaveChanges();
        transacao.Commit();
        return true;
    }
}
