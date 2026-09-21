using System.Globalization;
using VarthexComanda.Desktop.Catalogo;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Catalogo;

public class BooleanoParaSimNaoConverterTests
{
    private readonly BooleanoParaSimNaoConverter _conversor = new();

    private object? Converter(object? valor) =>
        _conversor.Convert(valor, typeof(string), null!, CultureInfo.InvariantCulture);

    [Fact]
    public void Verdadeiro_ViraSim() => Assert.Equal("Sim", Converter(true));

    [Fact]
    public void Falso_ViraNao() => Assert.Equal("Não", Converter(false));

    [Fact]
    public void Nulo_ViraNao() => Assert.Equal("Não", Converter(null));

    [Fact]
    public void TipoErrado_ViraNao() => Assert.Equal("Não", Converter("true"));

    [Fact]
    public void ConverterDeVolta_NaoSuportado() =>
        Assert.Throws<NotSupportedException>(() =>
            _conversor.ConvertBack("Sim", typeof(bool), null!, CultureInfo.InvariantCulture));
}
