namespace VarthexComanda.MassaMinima;

/// <summary>Catalogo generico de uma lanchonete ficticia (sem dados reais). Precos em centavos.</summary>
internal static class CatalogoDeExemplo
{
    public const string NomeDoEstabelecimento = "Lanchonete Exemplo";
    public const int QuantidadeDeComandas = 20;
    public const string CategoriaInativa = "Descontinuados";

    public sealed record ProdutoExemplo(string Nome, long PrecoCentavos);

    /// <summary>Categorias na ordem de cadastro; a ultima e desativada ao fim do historico.</summary>
    public static readonly IReadOnlyList<(string Categoria, ProdutoExemplo[] Produtos)> Categorias =
    [
        ("Lanches",
        [
            new ProdutoExemplo("X-Burger", 1800),
            new ProdutoExemplo("X-Salada", 2000),
            new ProdutoExemplo("X-Bacon", 2400),
            new ProdutoExemplo("X-Frango", 2200),
            new ProdutoExemplo("Hambúrguer Simples", 1500),
            new ProdutoExemplo("Misto Quente", 1200),
            new ProdutoExemplo("Cachorro-Quente", 1400),
            new ProdutoExemplo("Sanduíche Natural", 1600),
            new ProdutoExemplo("Wrap de Frango", 2100),
        ]),
        ("Bebidas",
        [
            new ProdutoExemplo("Refrigerante Lata", 600),
            new ProdutoExemplo("Refrigerante 600ml", 800),
            new ProdutoExemplo("Suco de Laranja", 900),
            new ProdutoExemplo("Suco de Maracujá", 900),
            new ProdutoExemplo("Água Mineral", 400),
            new ProdutoExemplo("Água com Gás", 500),
            new ProdutoExemplo("Café Expresso", 500),
            new ProdutoExemplo("Chá Gelado", 700),
            new ProdutoExemplo("Cerveja Long Neck", 1200),
        ]),
        ("Porções",
        [
            new ProdutoExemplo("Batata Frita", 2800),
            new ProdutoExemplo("Batata com Cheddar", 3400),
            new ProdutoExemplo("Mandioca Frita", 2600),
            new ProdutoExemplo("Anéis de Cebola", 2700),
            new ProdutoExemplo("Isca de Frango", 3600),
            new ProdutoExemplo("Calabresa Acebolada", 3200),
        ]),
        ("Sobremesas",
        [
            new ProdutoExemplo("Pudim", 1000),
            new ProdutoExemplo("Brownie", 1300),
            new ProdutoExemplo("Sorvete 2 Bolas", 1400),
            new ProdutoExemplo("Mousse de Maracujá", 1100),
            new ProdutoExemplo("Açaí 300ml", 1800),
        ]),
        (CategoriaInativa,
        [
            new ProdutoExemplo("Torta de Limão", 1200),
        ]),
    ];
}
