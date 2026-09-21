using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Application.Tests.Atendimento;

public class AbrirComandaTests
{
    [Fact]
    [Trait("Caso", "CT01")]
    public void Executar_NumeroValido_AbreComanda()
    {
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()));

        var resultado = caso.Executar(10);

        Assert.True(resultado.Sucesso);
        Assert.Equal(10, resultado.Valor!.Numero);
        Assert.Equal(StatusComanda.Aberta, resultado.Valor.Status);
    }

    [Fact]
    public void Executar_NumeroZeroOuNegativo_Falha()
    {
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()));

        var resultado = caso.Executar(0);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Informe um número de comanda válido.", resultado.Erros);
    }

    [Fact]
    [Trait("Caso", "CT02")]
    public void Executar_NumeroJaAberto_Falha()
    {
        var repositorio = new FakeComandaRepository();
        var caso = new AbrirComanda(repositorio, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()));
        caso.Executar(10);

        var resultado = caso.Executar(10);

        Assert.False(resultado.Sucesso);
        Assert.Contains("Já existe uma comanda aberta com esse número.", resultado.Erros);
    }

    [Fact]
    public void Executar_SemConfiguracao_NumeroAltoContinuaFuncionando()
    {
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()));

        var resultado = caso.Executar(9999);

        Assert.True(resultado.Sucesso);
    }

    [Fact]
    public void Executar_ComConfiguracao_NumeroAcimaDoLimiteFalha()
    {
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("comandas.quantidade_maxima", "30", DateTime.UtcNow);
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock(), new ObterConfiguracao(configuracoes));

        var resultado = caso.Executar(31);

        Assert.False(resultado.Sucesso);
        Assert.Contains("O número da comanda deve ser no máximo 30.", resultado.Erros);
    }

    [Fact]
    public void Executar_ComConfiguracao_NumeroDentroDoLimitePassa()
    {
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("comandas.quantidade_maxima", "30", DateTime.UtcNow);
        var caso = new AbrirComanda(new FakeComandaRepository(), new FakeClock(), new ObterConfiguracao(configuracoes));

        var resultado = caso.Executar(30);

        Assert.True(resultado.Sucesso);
    }
}
