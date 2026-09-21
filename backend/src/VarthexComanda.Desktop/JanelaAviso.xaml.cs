using System.Windows;
using System.Windows.Media;

namespace VarthexComanda.Desktop;

/// <summary>Janela de aviso de toque (botao grande, foco inicial no OK, Enter/Esc fecham).</summary>
public partial class JanelaAvisoView : Window
{
    public JanelaAvisoView(string titulo, string mensagem, TipoAviso tipo = TipoAviso.Informacao)
    {
        InitializeComponent();
        Title = titulo;
        TextoMensagem.Text = mensagem;
        FaixaTipo.Background = new SolidColorBrush(CorDoTipo(tipo));
    }

    internal static Color CorDoTipo(TipoAviso tipo) => tipo switch
    {
        TipoAviso.Erro => (Color)ColorConverter.ConvertFromString("#B23B32"),
        TipoAviso.Aviso => (Color)ColorConverter.ConvertFromString("#D9822B"),
        _ => (Color)ColorConverter.ConvertFromString("#1F5FBF")
    };

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}

/// <summary>Substitui a caixa de mensagem nativa por uma janela de aviso adequada a toque.</summary>
public static class JanelaAviso
{
    public static void Mostrar(string titulo, string mensagem, TipoAviso tipo = TipoAviso.Informacao)
    {
        var janela = new JanelaAvisoView(titulo, mensagem, tipo);
        // Qualificado: dentro de VarthexComanda.Desktop, "Application" solto resolve para o namespace VarthexComanda.Application.
        var dono = System.Windows.Application.Current?.MainWindow;
        if (dono is not null && dono.IsLoaded && dono.IsVisible)
        {
            janela.Owner = dono;
        }
        else
        {
            // sem janela principal (ex.: segunda instancia): centro da tela
            janela.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        janela.ShowDialog();
    }
}
