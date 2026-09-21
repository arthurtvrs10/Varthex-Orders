using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace VarthexComanda.Desktop.Catalogo;

public partial class ProdutosView : UserControl
{
    public ProdutosView(ProdutosViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!e.Handled && e.Key == Key.F
            && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control)
            && !e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            CampoBuscaProduto.Focus();
            CampoBuscaProduto.SelectAll();
            e.Handled = true;
        }
    }

    private void EscolherFoto_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Escolher foto do produto",
            Filter = "Imagens (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
        };

        if (dialogo.ShowDialog() == true && DataContext is ProdutosViewModel viewModel)
        {
            viewModel.DefinirFoto(dialogo.FileName);
        }
    }
}
