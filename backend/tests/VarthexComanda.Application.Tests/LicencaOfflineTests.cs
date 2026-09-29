using System.Security.Cryptography;
using VarthexComanda.Application.Licenciamento;

namespace VarthexComanda.Application.Tests;

public class LicencaOfflineTests : IDisposable
{
    private readonly RSA _rsa = RSA.Create(2048);
    private static readonly DateTimeOffset Inicio = new(2026, 1, 31, 0, 0, 0, TimeSpan.FromHours(-3));
    private DadosLicenca Dados(int meses) => new(1, "id", "computador", "Loja", Inicio, meses == 0 ? null : Inicio.AddMonths(meses));
    private string Emitir(DadosLicenca dados) => LicencaOffline.Emitir(dados, _rsa.ExportPkcs8PrivateKeyPem());
    private DadosLicenca Validar(string chave, DateTimeOffset agora, string pc = "computador", DateTimeOffset? ultimo = null)
        => LicencaOffline.Validar(chave, _rsa.ExportSubjectPublicKeyInfoPem(), pc, agora, ultimo);

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void PrazoTemLimiteExclusivoESobreviveReutilizacao(int meses)
    {
        var dados = Dados(meses);
        var chave = Emitir(dados);
        Assert.Equal(dados, Validar(chave, dados.Vencimento!.Value.AddTicks(-1)));
        Assert.Throws<InvalidOperationException>(() => Validar(chave, dados.Vencimento.Value));
        Assert.Throws<InvalidOperationException>(() => Validar(chave, dados.Vencimento.Value.AddYears(1)));
    }

    [Fact] public void VitaliciaNaoExpira() => Assert.Null(Validar(Emitir(Dados(0)), Inicio.AddYears(50)).Vencimento);
    [Fact] public void OutroComputadorRecusado() => Assert.Throws<InvalidOperationException>(() => Validar(Emitir(Dados(1)), Inicio, "outro"));
    [Fact] public void InicioFuturoRecusado() => Assert.Throws<InvalidOperationException>(() => Validar(Emitir(Dados(1)), Inicio.AddTicks(-1)));
    [Fact] public void RelogioAtrasadoRecusado() => Assert.Throws<InvalidOperationException>(() => Validar(Emitir(Dados(0)), Inicio, ultimo: Inicio.AddHours(1)));
    [Fact] public void AjustePequenoDoRelogioTolerado() => Assert.NotNull(Validar(Emitir(Dados(1)), Inicio, ultimo: Inicio.AddMinutes(4)));
    [Fact] public void RenovacaoPermiteUsoDepoisDoPrazoAnterior()
    {
        var agora = Inicio.AddMonths(1);
        Assert.Throws<InvalidOperationException>(() => Validar(Emitir(Dados(1)), agora));
        Assert.NotNull(Validar(Emitir(Dados(1) with { Inicio = agora, Vencimento = agora.AddMonths(2) }), agora));
    }
    [Fact] public void DadosAlteradosSemNovaAssinaturaRecusados()
    {
        var chave = Emitir(Dados(1)).Split('.');
        var vitalicia = Emitir(Dados(0)).Split('.');
        Assert.Throws<InvalidOperationException>(() => Validar(vitalicia[0] + "." + chave[1], Inicio));
    }
    [Fact] public void EmissorNaoAutorizadoRecusado()
    {
        using var outro = RSA.Create(2048);
        var chave = LicencaOffline.Emitir(Dados(0), outro.ExportPkcs8PrivateKeyPem());
        Assert.Throws<InvalidOperationException>(() => Validar(chave, Inicio));
    }
    [Theory] [InlineData("")] [InlineData("abc.def")] [InlineData("a.b.c")]
    public void ChaveMalformadaRecusada(string chave) => Assert.Throws<InvalidOperationException>(() => Validar(chave, Inicio));
    public void Dispose() => _rsa.Dispose();
}
