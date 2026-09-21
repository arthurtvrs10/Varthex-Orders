using System.Windows.Controls;
using System.Windows.Input;

namespace VarthexComanda.Desktop.Atendimento;

public partial class HistoricoView : UserControl
{
    public HistoricoViewModel ViewModel { get; }

    public HistoricoView(HistoricoViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!e.Handled && e.Key == Key.F
            && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control)
            && !e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            CampoBuscaNumero.Focus();
            CampoBuscaNumero.SelectAll();
            e.Handled = true;
        }
    }
}
