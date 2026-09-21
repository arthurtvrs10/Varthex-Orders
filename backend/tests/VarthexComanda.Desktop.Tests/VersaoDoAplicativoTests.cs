using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using VarthexComanda.Desktop.Configuracao;
using Xunit;

namespace VarthexComanda.Desktop.Tests;

public class VersaoDoAplicativoTests
{
    [Fact]
    public void Atual_E1Ponto0Ponto0_SemSufixoDeRevisao()
    {
        Assert.Equal("1.0.0", VersaoDoAplicativo.Atual);
    }

    [Fact]
    public void Configuracao_VersaoTexto_MostraAVersaoAtual()
    {
        var repositorio = new FakeConfiguracaoRepository();
        var viewModel = new ConfiguracaoViewModel(
            new ObterConfiguracao(repositorio),
            new SalvarConfiguracao(repositorio, new FakeClock()));

        Assert.Equal("Versão 1.0.0", viewModel.VersaoTexto);
    }
}
