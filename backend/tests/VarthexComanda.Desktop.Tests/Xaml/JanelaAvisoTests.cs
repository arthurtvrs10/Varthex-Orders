using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Xaml;

/// <summary>Janela de aviso de toque no lugar da MessageBox nativa (sem ShowDialog: so construir e medir).</summary>
[Trait("Requisito", "RNF18")]
public class JanelaAvisoTests
{
    [Fact]
    public void JanelaAvisoView_CarregaComOkPadraoECancelaEFocoInicial()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var janela = new JanelaAvisoView("Varthex Comanda", "Mensagem de teste", TipoAviso.Informacao);
            CarregamentoDeXamlTests.MedirEOrganizar((FrameworkElement)janela.Content);

            var ok = Assert.IsType<Button>(janela.FindName("BotaoOk"));
            var texto = Assert.IsType<TextBlock>(janela.FindName("TextoMensagem"));

            Assert.Equal("Varthex Comanda", janela.Title);
            Assert.Equal("Mensagem de teste", texto.Text);
            Assert.Equal("OK", ok.Content);
            Assert.True(ok.IsDefault);
            Assert.True(ok.IsCancel);
            Assert.Equal(56, ok.Height);
            Assert.Equal(16, texto.FontSize);
            Assert.Equal(TextWrapping.Wrap, texto.TextWrapping);
            Assert.Same(ok, System.Windows.Input.FocusManager.GetFocusedElement(janela));
            Assert.Equal(560, janela.Width);
            Assert.Equal(ResizeMode.NoResize, janela.ResizeMode);
        });
    }

    [Theory]
    [InlineData(TipoAviso.Informacao, "#FF1F5FBF")]
    [InlineData(TipoAviso.Aviso, "#FFD9822B")]
    [InlineData(TipoAviso.Erro, "#FFB23B32")]
    public void JanelaAvisoView_FaixaLateralTemACorDoTipo(TipoAviso tipo, string corEsperada)
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var janela = new JanelaAvisoView("t", "m", tipo);
            var faixa = Assert.IsType<Border>(janela.FindName("FaixaTipo"));
            var pincel = Assert.IsType<SolidColorBrush>(faixa.Background);
            Assert.Equal(corEsperada, pincel.Color.ToString());
        });
    }

    [Fact]
    public void NoCodigoDoApp_NaoHaMessageBoxNativa()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !Directory.Exists(Path.Combine(pasta.FullName, "src", "VarthexComanda.Desktop")))
        {
            pasta = pasta.Parent;
        }
        Assert.NotNull(pasta);

        var usos = Directory
            .EnumerateFiles(Path.Combine(pasta!.FullName, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadAllText(f).Contains("MessageBox.Show"))
            .ToList();

        Assert.Empty(usos);
    }
}
