using VarthexComanda.Application.Atendimento;

namespace VarthexComanda.Application.Tests.Atendimento;

public class DefinirNomeClienteTests
{
    [Theory]
    [InlineData("  João da Silva  ", "João da Silva")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void NomeOpcionalENormalizado(string? entrada, string? esperado)
    {
        var repo = new FakeComandaRepository();
        var comanda = repo.AbrirComanda(1, DateTime.UtcNow);
        var resultado = new DefinirNomeCliente(repo).Executar(comanda.Id, entrada);
        Assert.True(resultado.Sucesso);
        Assert.Equal(esperado, repo.BuscarComItens(comanda.Id)!.Comanda.NomeCliente);
    }

    [Fact]
    public void NomeInvalidoNaoSubstituiNomeSalvo()
    {
        var repo = new FakeComandaRepository();
        var comanda = repo.AbrirComanda(1, DateTime.UtcNow);
        var caso = new DefinirNomeCliente(repo);
        caso.Executar(comanda.Id, "Ana");
        Assert.False(caso.Executar(comanda.Id, new string('a', 81)).Sucesso);
        Assert.False(caso.Executar(comanda.Id, "Ana\nMaria").Sucesso);
        Assert.Equal("Ana", comanda.NomeCliente);
    }

    [Fact]
    public void ComandaCanceladaNaoPodeSerRenomeada()
    {
        var repo = new FakeComandaRepository();
        var comanda = repo.AbrirComanda(1, DateTime.UtcNow);
        repo.CancelarComanda(comanda.Id, DateTime.UtcNow);
        Assert.False(new DefinirNomeCliente(repo).Executar(comanda.Id, "Ana").Sucesso);
    }
}
