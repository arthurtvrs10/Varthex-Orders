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
        // Constante intencional: ao mudar <Version> no csproj, atualize aqui de proposito.
        Assert.Equal("1.0.0", VersaoDoAplicativo.Atual);
    }

    [Fact]
    public void Configuracao_VersaoTexto_MostraAVersaoAtual()
    {
        var repositorio = new FakeConfiguracaoRepository();
        var viewModel = new ConfiguracaoViewModel(
            new ObterConfiguracao(repositorio),
            new SalvarConfiguracao(repositorio, new FakeClock()));

        Assert.Matches(@"^\d+\.\d+\.\d+$", VersaoDoAplicativo.Atual);
        Assert.Equal($"Versão {VersaoDoAplicativo.Atual}", viewModel.VersaoTexto);
    }
}
