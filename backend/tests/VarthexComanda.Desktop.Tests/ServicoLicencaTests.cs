using System.IO;
using System.Security.Cryptography;
using VarthexComanda.Application.Licenciamento;
using VarthexComanda.Desktop.Licenciamento;
using VarthexComanda.Desktop.Tests.Xaml;
using System.Windows;
using System.Windows.Controls;

namespace VarthexComanda.Desktop.Tests;

public class ServicoLicencaTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "VarthexLicencaTest-" + Guid.NewGuid().ToString("N"));
    private readonly RSA _rsa = RSA.Create(2048);
    private ServicoLicenca Servico => new(Path.Combine(_pasta, "estado.dat"), _rsa.ExportSubjectPublicKeyInfoPem(), "pc");
    private string Chave(string pc = "pc") => LicencaOffline.Emitir(new DadosLicenca(1, "id", pc, "Teste", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddMonths(1)), _rsa.ExportPkcs8PrivateKeyPem());

    [Fact]
    public void AtivacaoPersisteProtegidaEReabre()
    {
        var chave = Chave();
        Servico.Verificar(chave);
        Assert.Equal("Teste", Servico.Verificar().Cliente);
        Assert.DoesNotContain(chave, File.ReadAllText(Path.Combine(_pasta, "estado.dat")));
    }

    [Fact]
    public void RenovacaoInvalidaPreservaLicencaAtual()
    {
        Servico.Verificar(Chave());
        Assert.Throws<InvalidOperationException>(() => Servico.Verificar(Chave("outro")));
        Assert.Equal("pc", Servico.Verificar().Computador);
    }

    [Fact]
    public void EstadoCorrompidoNaoLiberaAcesso()
    {
        Servico.Verificar(Chave());
        File.WriteAllBytes(Path.Combine(_pasta, "estado.dat"), [1, 2, 3]);
        Assert.Throws<InvalidOperationException>(() => Servico.Verificar());
    }

    [Fact]
    public void SemLicencaNaoLiberaAcesso() => Assert.Throws<InvalidOperationException>(() => Servico.Verificar());

    [Fact]
    public void DialogoAtivaChaveEFechaComSucesso()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var janela = new JanelaLicenca(Servico, "Ativação de teste");
            var painel = (StackPanel)((ScrollViewer)janela.Content).Content;
            var entrada = painel.Children.OfType<TextBox>().Single(t => !t.IsReadOnly);
            var botao = painel.Children.OfType<Button>().Single(b => (string)b.Content == "Ativar licença");
            janela.Loaded += (_, _) =>
            {
                entrada.Text = Chave();
                botao.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            Assert.True(janela.ShowDialog());
            Assert.Equal("Teste", Servico.Verificar().Cliente);
        });
    }

    public void Dispose()
    {
        _rsa.Dispose();
        if (Directory.Exists(_pasta)) Directory.Delete(_pasta, true);
    }
}
