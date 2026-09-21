using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class AdicionarItemTests
{
    private static (FakeComandaRepository comandas, FakeProdutoRepository produtos, int comandaId, int produtoAtivoId, int produtoInativoId) Preparar()
    {
        var comandas = new FakeComandaRepository();
        var comanda = new AbrirComanda(comandas, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository())).Executar(10).Valor!;

        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var cadastrarProduto = new CadastrarProduto(produtos, categorias, new FakeClock());
        var ativo = cadastrarProduto.Executar("Refrigerante", categoria.Id, 500).Valor!;
        var inativo = cadastrarProduto.Executar("Descontinuado", categoria.Id, 300).Valor!;
        new DesativarProduto(produtos, new FakeClock()).Executar(inativo.Id);

        return (comandas, produtos, comanda.Id, ativo.Id, inativo.Id);
    }

    [Fact]
    public void Executar_ProdutoAtivo_AdicionaItemERecalculaTotal()
    {
        var (comandas, produtos, comandaId, produtoId, _) = Preparar();
        var caso = new AdicionarItem(comandas, produtos, new FakeClock());

        var resultado = caso.Executar(comandaId, produtoId, 2);

        Assert.True(resultado.Sucesso);
        Assert.Single(resultado.Valor!.Itens);
        Assert.Equal(2, resultado.Valor.Itens[0].Quantidade);
        Assert.Equal(1000, resultado.Valor.Itens[0].SubtotalCentavos);
        Assert.Equal(1000, resultado.Valor.Comanda.TotalCentavos);
    }

    [Fact]
    [Trait("Caso", "CT03")]
    public void Executar_DuasVezesMesmoProduto_IncrementaQuantidadeEmVezDeDuplicar()
    {
        var (comandas, produtos, comandaId, produtoId, _) = Preparar();
        var caso = new AdicionarItem(comandas, produtos, new FakeClock());
        caso.Executar(comandaId, produtoId, 1);

        var resultado = caso.Executar(comandaId, produtoId, 1);

        Assert.Single(resultado.Valor!.Itens);
        Assert.Equal(2, resultado.Valor.Itens[0].Quantidade);
        Assert.Equal(1000, resultado.Valor.Comanda.TotalCentavos);
    }

    [Fact]
    [Trait("Caso", "CT06")]
    public void Executar_QuantidadeZeroOuNegativa_Falha()
    {
        var (comandas, produtos, comandaId, produtoId, _) = Preparar();
        var caso = new AdicionarItem(comandas, produtos, new FakeClock());

        var resultado = caso.Executar(comandaId, produtoId, 0);

        Assert.False(resultado.Sucesso);
        Assert.Contains("A quantidade deve ser maior que zero.", resultado.Erros);
    }

    [Fact]
    public void Executar_ProdutoInativo_Falha()
    {
        var (comandas, produtos, comandaId, _, produtoInativoId) = Preparar();
        var caso = new AdicionarItem(comandas, produtos, new FakeClock());

        var resultado = caso.Executar(comandaId, produtoInativoId, 1);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Produto indisponível.", resultado.Erros);
    }

    [Fact]
    public void Executar_ComandaCancelada_Falha()
    {
        var (comandas, produtos, comandaId, produtoId, _) = Preparar();
        new CancelarComanda(comandas, new FakeClock()).Executar(comandaId);
        var caso = new AdicionarItem(comandas, produtos, new FakeClock());

        var resultado = caso.Executar(comandaId, produtoId, 1);

        Assert.False(resultado.Sucesso);
        Assert.Contains("A comanda não está aberta.", resultado.Erros);
    }
}
