using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class EncerrarComandaTests
{
    [Fact]
    [Trait("Caso", "CT10")]
    public void Executar_ComandaComItens_RetornaVendaEFechaComanda()
    {
        var comandas = new FakeComandaRepository();
        var relogio = new FakeClock();
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        var produto = new Produto { Id = 1, CategoriaId = 1, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
        comandas.AdicionarItem(comanda.Id, produto, 2, relogio.UtcNow);
        var caso = new EncerrarComanda(comandas, relogio);

        var resultado = caso.Executar(comanda.Id);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1000, resultado.Valor!.TotalCentavos);
        Assert.Empty(comandas.ListarAbertas());
    }

    [Fact]
    [Trait("Caso", "CT07")]
    public void Executar_ComandaVazia_Falha()
    {
        var comandas = new FakeComandaRepository();
        var relogio = new FakeClock();
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        var caso = new EncerrarComanda(comandas, relogio);

        var resultado = caso.Executar(comanda.Id);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Adicione um item antes de encerrar.", resultado.Erros[0]);
        Assert.Single(comandas.ListarAbertas());
    }

    [Fact]
    public void Executar_ComandaInexistente_Falha()
    {
        var comandas = new FakeComandaRepository();
        var relogio = new FakeClock();
        var caso = new EncerrarComanda(comandas, relogio);

        var resultado = caso.Executar(999);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Comanda não encontrada.", resultado.Erros[0]);
    }

    [Fact]
    public void Executar_ComandaJaFechada_Falha()
    {
        var comandas = new FakeComandaRepository();
        var relogio = new FakeClock();
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        var produto = new Produto { Id = 1, CategoriaId = 1, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
        comandas.AdicionarItem(comanda.Id, produto, 1, relogio.UtcNow);
        comandas.EncerrarComanda(comanda.Id, relogio.UtcNow);
        var caso = new EncerrarComanda(comandas, relogio);

        var resultado = caso.Executar(comanda.Id);

        Assert.False(resultado.Sucesso);
        Assert.Equal("A comanda não está aberta.", resultado.Erros[0]);
    }
}
