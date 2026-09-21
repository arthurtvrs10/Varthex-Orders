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
            ViewModel.AdicionarPrimeiroDaBuscaCommand.Execute(null);
            CampoBusca.SelectAll();
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

        switch (e.Key)
        {
            case Key.Up or Key.Down when !EstaNoMenu(e):
                // no menu, as setas continuam movendo o foco entre os cards e categorias
                (e.Key == Key.Up ? ViewModel.SelecionarItemAnteriorCommand : ViewModel.SelecionarProximoItemCommand).Execute(null);
                e.Handled = true;
                break;
            case Key.OemPlus or Key.Add:
                ViewModel.AumentarSelecionadoCommand.Execute(null);
                e.Handled = true;
                RestaurarFocoSePerdido();
                break;
            case Key.OemMinus or Key.Subtract:
                ViewModel.DiminuirSelecionadoCommand.Execute(null);
                e.Handled = true;
                RestaurarFocoSePerdido();
                break;
            case Key.Delete:
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
    private void RestaurarFocoSePerdido()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (ViewModel.ComandaAtual is not null && !IsKeyboardFocusWithin)
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
