using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class AlterarQuantidadeTests
{
    [Fact]
    [Trait("Caso", "CT05")]
    public void Executar_QuantidadeValida_RecalculaSubtotalETotal()
    {
        var comandas = new FakeComandaRepository();
        var comanda = new AbrirComanda(comandas, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository())).Executar(10).Valor!;
        var categorias = new FakeCategoriaRepository();
        var categoria = new CadastrarCategoria(categorias, new FakeClock()).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        var produto = new CadastrarProduto(produtos, categorias, new FakeClock()).Executar("Refrigerante", categoria.Id, 500).Valor!;
        var item = new AdicionarItem(comandas, produtos, new FakeClock()).Executar(comanda.Id, produto.Id, 3).Valor!.Itens[0];
        var caso = new AlterarQuantidade(comandas, new FakeClock());

        var resultado = caso.Executar(item.Id, 2);

        Assert.True(resultado.Sucesso);
        Assert.Equal(2, resultado.Valor!.Itens[0].Quantidade);
        Assert.Equal(1000, resultado.Valor.Itens[0].SubtotalCentavos);
        Assert.Equal(1000, resultado.Valor.Comanda.TotalCentavos);
    }

    [Fact]
    [Trait("Caso", "CT06")]
    public void Executar_QuantidadeZeroOuNegativa_Falha()
    {
        var caso = new AlterarQuantidade(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(1, 0);

        Assert.False(resultado.Sucesso);
        Assert.Contains("A quantidade deve ser maior que zero.", resultado.Erros);
    }

    [Fact]
    public void Executar_ItemInexistente_Falha()
    {
        var caso = new AlterarQuantidade(new FakeComandaRepository(), new FakeClock());

        var resultado = caso.Executar(999, 1);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Item não encontrado.", resultado.Erros);
    }
}
