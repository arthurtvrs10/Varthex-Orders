using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Atendimento;

public partial class AtendimentoView : UserControl
{
    public AtendimentoViewModel ViewModel { get; }

    public AtendimentoView(AtendimentoViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        DataObject.AddPastingHandler(CampoNumeroComanda, CampoNumero_Colar);
        // as linhas sao recriadas a cada alteracao; garante que o teclado continue chegando (F4/Esc)
        viewModel.Itens.CollectionChanged += (_, _) => RestaurarFocoSePerdido();
    }

    // ---- foco inicial: so depois que o elemento esta de fato visivel (focar antes falha em silencio) ----

    private void PainelGrade_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => CampoNumeroComanda.Focus()));
        }
    }

    private void PainelEdicao_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            // a busca substitui o antigo foco em PainelComanda; o F4 segue valendo porque ela esta dentro deste UserControl
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => CampoBusca.Focus()));
        }
    }

    // ---- numero da comanda: somente digitos ----

    private void CampoNumero_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !SomenteDigitos(e.Text);
    }

    private void CampoNumero_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // o espaco nao passa por PreviewTextInput
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private void CampoNumero_Colar(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string texto || !SomenteDigitos(texto))
        {
            e.CancelCommand();
        }
    }

    public static bool SomenteDigitos(string? texto) =>
        !string.IsNullOrEmpty(texto) && texto.All(char.IsAsciiDigit);

    // ---- teclado do atendimento ----

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        var modificadores = e.KeyboardDevice.Modifiers;
        var ctrl = modificadores.HasFlag(ModifierKeys.Control);

        if (ctrl && !modificadores.HasFlag(ModifierKeys.Alt))
        {
            // somente Ctrl puro: Ctrl+Shift+N/F nao sao atalhos nossos
            if (modificadores != ModifierKeys.Control)
            {
                return;
            }

            if (e.Key == Key.N && ViewModel.ComandaAtual is null)
            {
                CampoNumeroComanda.Focus();
                CampoNumeroComanda.SelectAll();
                e.Handled = true;
            }
            else if (e.Key == Key.F && ViewModel.ComandaAtual is not null)
            {
                CampoBusca.Focus();
                CampoBusca.SelectAll();
                e.Handled = true;
            }
            return;
        }

        if (ViewModel.ComandaAtual is null || modificadores.HasFlag(ModifierKeys.Alt))
        {
            return;
        }

        var emBusca = ReferenceEquals(e.OriginalSource, CampoBusca);

        if (e.Key == Key.Enter && emBusca)
        {
            // Enter mantido pressionado repete o KeyDown: so a primeira pressao adiciona
            if (!e.IsRepeat)
            {
                ViewModel.AdicionarPrimeiroDaBuscaCommand.Execute(null);
                CampoBusca.SelectAll();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (emBusca && !string.IsNullOrEmpty(CampoBusca.Text))
            {
                ViewModel.TextoBuscaCatalogo = string.Empty;
            }
            else
            {
                ViewModel.FecharEdicaoCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        // teclas de item nunca disparam com o foco numa caixa de texto (+ e - sao texto)
        if (EstaEmCampoDeTexto(e))
        {
            return;
        }

        // Teclas de item: nunca no menu (cards/categorias mantem o foco proprio) nem com modificadores.
        // O "+" da linha principal e digitado com Shift em varios layouts, entao OemPlus tolera Shift.
        var semModificador = modificadores == ModifierKeys.None;
        var teclaMaisComShift = e.Key == Key.OemPlus && modificadores == ModifierKeys.Shift;
        if (EstaNoMenu(e) || !(semModificador || teclaMaisComShift))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Up or Key.Down when semModificador:
                (e.Key == Key.Up ? ViewModel.SelecionarItemAnteriorCommand : ViewModel.SelecionarProximoItemCommand).Execute(null);
                e.Handled = true;
                break;
            case Key.OemPlus or Key.Add when semModificador || teclaMaisComShift:
                ViewModel.AumentarSelecionadoCommand.Execute(null);
                e.Handled = true;
                RestaurarFocoSePerdido();
                break;
            case Key.OemMinus or Key.Subtract when semModificador:
                ViewModel.DiminuirSelecionadoCommand.Execute(null);
                e.Handled = true;
                RestaurarFocoSePerdido();
                break;
            case Key.Delete when semModificador:
                ViewModel.RemoverSelecionadoCommand.Execute(null);
                e.Handled = true;
                RestaurarFocoSePerdido();
                break;
        }
    }

    private static bool EstaEmCampoDeTexto(KeyEventArgs e) =>
        e.OriginalSource is TextBoxBase || Keyboard.FocusedElement is TextBoxBase;

    private bool EstaNoMenu(KeyEventArgs e) =>
        e.OriginalSource is DependencyObject origem && PainelMenu.IsAncestorOf(origem);

    // As linhas sao recriadas a cada alteracao; se o foco estava num botao da linha ele some junto e o
    // teclado (inclusive o F4) deixaria de chegar aqui. Recolhe o foco no painel da comanda.
    private bool _restauracaoDeFocoPendente;

    private void RestaurarFocoSePerdido()
    {
        if (_restauracaoDeFocoPendente)
        {
            return;
        }

        _restauracaoDeFocoPendente = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            _restauracaoDeFocoPendente = false;

            // so quando o foco ficou "solto" (nulo ou na propria janela): nunca rouba foco de outra janela
            // (ex.: dialogo de encerramento) nem de um controle que o usuario escolheu
            var foco = Keyboard.FocusedElement;
            var perdido = foco is null || foco is Window;
            if (ViewModel.ComandaAtual is not null && perdido && !IsKeyboardFocusWithin
                && Window.GetWindow(this)?.IsActive == true)
            {
                PainelComanda.Focus();
            }
        }));
    }

    // ---- selecao da linha (por Tab nos botoes da linha ou por clique) ----

    private void LinhaItem_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        SelecionarLinha(sender);

    private void LinhaItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        SelecionarLinha(sender);

    private void SelecionarLinha(object linha)
    {
        if (linha is FrameworkElement { DataContext: ItemComanda item })
        {
            ViewModel.ItemSelecionadoId = item.Id;
        }
    }
}
