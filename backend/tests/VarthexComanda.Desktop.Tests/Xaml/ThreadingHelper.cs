using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace VarthexComanda.Desktop.Tests.Xaml;

/// <summary>
/// WPF exige thread STA e so admite UM <c>Application</c> por processo, preso a thread que o criou.
/// Por isso todos os testes de XAML rodam numa unica thread STA dedicada (com Dispatcher), criada uma
/// vez por processo; classes de teste em paralelo ficam serializadas nela.
/// </summary>
internal static class ThreadingHelper
{
    private static readonly Lazy<Dispatcher> DispatcherSta = new(IniciarThreadSta);

    private static Dispatcher IniciarThreadSta()
    {
        Dispatcher? dispatcher = null;
        using var pronto = new ManualResetEventSlim(false);
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            pronto.Set();
            Dispatcher.Run();
        })
        {
            Name = "STA dos testes de XAML",
            IsBackground = true // nao segura o processo de teste aberto
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        pronto.Wait();
        return dispatcher!;
    }

    /// <summary>Executa <paramref name="acao"/> na thread STA dedicada e relanca a excecao original (com o stack de la).</summary>
    public static void EmSta(Action acao)
    {
        ExceptionDispatchInfo? falha = null;
        DispatcherSta.Value.Invoke(() =>
        {
            try
            {
                acao();
            }
            catch (Exception ex)
            {
                falha = ExceptionDispatchInfo.Capture(ex);
            }
        });
        falha?.Throw();
    }
}
