using VarthexComanda.Infrastructure.Concurrency;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class SingleInstanceGuardTests
{
    [Fact]
    [Trait("Caso", "CT19")]
    public void TryAcquire_SegundaGuardaComMesmoNome_FalhaAteAPrimeiraLiberar()
    {
        // Nota: Mutex do Windows é afim à thread que o adquiriu — ReleaseMutex só é válido
        // se chamado pela mesma thread que chamou WaitOne. Por isso "segunda"/"terceira"
        // rodam em threads dedicadas (via System.Threading.Thread + Join, não Task) em vez
        // de usar await Task.Run: uma continuação após "await" pode retomar em uma thread do
        // pool diferente da que adquiriu o mutex de "primeira", o que faria o Release() de
        // "primeira" lançar ApplicationException. Usar Thread.Join evita tanto esse problema
        // quanto o bloqueio Task.Result sinalizado pelo xUnit1031.
        var nomeMutex = "VarthexComandaTests_" + Guid.NewGuid();
        using var primeira = new SingleInstanceGuard(nomeMutex);
        Assert.True(primeira.TryAcquire());

        var segundaConseguiu = false;
        var threadSegunda = new Thread(() =>
        {
            using var segunda = new SingleInstanceGuard(nomeMutex);
            segundaConseguiu = segunda.TryAcquire();
        });
        threadSegunda.Start();
        threadSegunda.Join();
        Assert.False(segundaConseguiu);

        primeira.Release();

        var terceiraConseguiu = false;
        var threadTerceira = new Thread(() =>
        {
            using var terceira = new SingleInstanceGuard(nomeMutex);
            terceiraConseguiu = terceira.TryAcquire();
            if (terceiraConseguiu) terceira.Release();
        });
        threadTerceira.Start();
        threadTerceira.Join();
        Assert.True(terceiraConseguiu);
    }
}
