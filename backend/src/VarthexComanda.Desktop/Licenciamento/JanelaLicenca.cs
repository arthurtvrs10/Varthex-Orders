using System.Windows;
using System.Windows.Controls;

namespace VarthexComanda.Desktop.Licenciamento;

public sealed class JanelaLicenca : Window
{
    public JanelaLicenca(ServicoLicenca servico, string mensagem)
    {
        Title = "Ativação — Varthex Comanda";
        Width = 650; Height = 540; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var painel = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = painel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        painel.Children.Add(new TextBlock { Text = "Licença do Varthex Comanda", FontSize = 24, Margin = new Thickness(0, 0, 0, 16) });
        var aviso = new TextBlock { Text = mensagem, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) };
        painel.Children.Add(aviso);
        painel.Children.Add(new TextBlock { Text = "Envie este código ao responsável pela licença:" });
        painel.Children.Add(new TextBox { Text = servico.Computador, IsReadOnly = true, TextWrapping = TextWrapping.Wrap });
        var copiar = new Button { Content = "Copiar código do computador", Margin = new Thickness(0, 8, 0, 16) };
        copiar.Click += (_, _) => { try { Clipboard.SetText(servico.Computador); } catch { aviso.Text = "Selecione o código e copie com Ctrl+C."; } };
        painel.Children.Add(copiar);
        painel.Children.Add(new TextBlock { Text = "Cole a chave recebida:" });
        var entrada = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        painel.Children.Add(entrada);
        var ativar = new Button { Content = "Ativar licença", Margin = new Thickness(0, 12, 0, 12) };
        ativar.Click += (_, _) =>
        {
            try { servico.Verificar(entrada.Text); DialogResult = true; }
            catch (Exception e) { aviso.Text = e is InvalidOperationException ? e.Message : "Não foi possível salvar a licença. Verifique as permissões da pasta de dados."; }
        };
        painel.Children.Add(ativar);
        painel.Children.Add(new TextBlock { Text = "A ativação funciona sem internet. Seus dados permanecem guardados ao vencer a licença.", TextWrapping = TextWrapping.Wrap });
    }
}
