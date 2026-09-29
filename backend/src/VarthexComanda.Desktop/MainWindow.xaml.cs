using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Desktop.Catalogo;
using VarthexComanda.Desktop.Configuracao;

namespace VarthexComanda.Desktop;

public partial class MainWindow : Window
{
    public static readonly RoutedCommand IrParaAtendimentoCommand = new();
    public static readonly RoutedCommand IrParaProdutosCommand = new();
    public static readonly RoutedCommand IrParaHistoricoCommand = new();
    public static readonly RoutedCommand IrParaBackupCommand = new();
    public static readonly RoutedCommand IrParaConfiguracoesCommand = new();

    private readonly AtendimentoView _atendimentoView;
    private readonly ProdutosView _produtosView;
    private readonly HistoricoView _historicoView;
    private readonly BackupView _backupView;
    private readonly ConfiguracaoView _configuracaoView;

    public MainWindow(AtendimentoView atendimentoView, ProdutosView produtosView, HistoricoView historicoView, BackupView backupView, ConfiguracaoView configuracaoView)
    {
        InitializeComponent();
        RegistrarAtalho(IrParaAtendimentoCommand, BotaoAtendimento, IrParaAtendimento);
        RegistrarAtalho(IrParaProdutosCommand, BotaoProdutos, IrParaProdutos);
        RegistrarAtalho(IrParaHistoricoCommand, BotaoHistorico, IrParaHistorico);
        RegistrarAtalho(IrParaBackupCommand, BotaoBackup, IrParaBackup);
        RegistrarAtalho(IrParaConfiguracoesCommand, BotaoConfiguracoes, IrParaConfiguracoes);
        _atendimentoView = atendimentoView;
        _produtosView = produtosView;
        _historicoView = historicoView;
        _backupView = backupView;
        _configuracaoView = configuracaoView;
        ConteudoPrincipal.Content = _atendimentoView;
    }

    /// <summary>
    /// Modo de restauração (banco corrompido): só a aba Backup fica disponível, com a faixa de
    /// aviso no topo. As demais abas ficam desabilitadas para que nada leia ou escreva no banco.
    /// </summary>
    public void EntrarModoRestauracao()
    {
        BotaoAtendimento.IsEnabled = false;
        BotaoProdutos.IsEnabled = false;
        BotaoHistorico.IsEnabled = false;
        BotaoConfiguracoes.IsEnabled = false;
        FaixaRestauracao.Visibility = Visibility.Visible;

        _backupView.ViewModel.ModoRestauracao = true;
        _backupView.ViewModel.AtualizarLista();
        ConteudoPrincipal.Content = _backupView;
    }

    // Atalhos Ctrl+1..5 seguem o estado do botao correspondente: no modo de restauracao as abas
    // desabilitadas nao respondem ao teclado (CanExecute = false).
    private void RegistrarAtalho(RoutedCommand comando, Button botao, Action acao)
    {
        CommandBindings.Add(new CommandBinding(
            comando,
            (_, _) => { if (botao.IsEnabled) acao(); },
            (_, e) => { e.CanExecute = botao.IsEnabled; e.Handled = true; }));
    }

    private void MostrarAtendimento_Click(object sender, RoutedEventArgs e) => IrParaAtendimento();

    private void MostrarProdutos_Click(object sender, RoutedEventArgs e) => IrParaProdutos();

    private void MostrarHistorico_Click(object sender, RoutedEventArgs e) => IrParaHistorico();

    private void MostrarBackup_Click(object sender, RoutedEventArgs e) => IrParaBackup();

    private void MostrarConfiguracao_Click(object sender, RoutedEventArgs e) => IrParaConfiguracoes();
    private void MostrarLicenca_Click(object sender, RoutedEventArgs e) => ((App)System.Windows.Application.Current).RenovarLicenca();

    private void IrParaAtendimento()
    {
        _atendimentoView.ViewModel.AtualizarCategorias();
        _atendimentoView.ViewModel.AtualizarComandasAbertas();
        MostrarConteudo(_atendimentoView);
    }

    private void IrParaProdutos() => MostrarConteudo(_produtosView);

    private void IrParaHistorico()
    {
        _historicoView.ViewModel.AtualizarVendas();
        MostrarConteudo(_historicoView);
    }

    private void IrParaBackup()
    {
        _backupView.ViewModel.AtualizarLista();
        MostrarConteudo(_backupView);
    }

    private void IrParaConfiguracoes()
    {
        _configuracaoView.ViewModel.Carregar();
        MostrarConteudo(_configuracaoView);
    }

    private void MostrarConteudo(UIElement view)
    {
        ConteudoPrincipal.Content = view;
        // Foco no primeiro controle util da tela; adiado ate a nova tela ser carregada.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            try
            {
                ConteudoPrincipal.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }
            catch (Exception)
            {
                // Foco inicial e apenas conveniencia: nunca deve derrubar a troca de tela.
            }
        }));
    }
}
