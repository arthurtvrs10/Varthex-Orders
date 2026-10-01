using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using VarthexComanda.Desktop.Atendimento;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Xaml;

/// <summary>
/// Atendimento por teclado (RNF18 / CT17) no nivel da view: controles existem e estao ligados, ordem de Tab,
/// destaque da linha selecionada e tratamento das teclas. Sem exibir janela (foco real de teclado nao e
/// testavel aqui; o foco inicial e conferido na verificacao por UI Automation).
/// </summary>
[Trait("Requisito", "RNF18")]
[Trait("Caso", "CT17")]
public class AtendimentoTecladoXamlTests
{
    private static IEnumerable<DependencyObject> Descendentes(DependencyObject raiz)
    {
        var quantidade = VisualTreeHelper.GetChildrenCount(raiz);
        for (var i = 0; i < quantidade; i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            yield return filho;
            foreach (var neto in Descendentes(filho))
            {
                yield return neto;
            }
        }
    }

    // Aproximacao da ordem de Tab do WPF: percurso em profundidade, irmaos ordenados por TabIndex
    // (estavel: empate = ordem do documento), ignorando subarvores nao visiveis e elementos que nao sao tab stop.
    private static IEnumerable<UIElement> OrdemDeTab(DependencyObject raiz)
    {
        var filhos = Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(raiz))
            .Select(i => VisualTreeHelper.GetChild(raiz, i))
            .OrderBy(f => f is UIElement u ? KeyboardNavigation.GetTabIndex(u) : int.MaxValue);

        foreach (var filho in filhos)
        {
            if (filho is UIElement { Visibility: not Visibility.Visible })
            {
                continue;
            }

            if (filho is UIElement elemento && elemento.Focusable && KeyboardNavigation.GetIsTabStop(elemento))
            {
                yield return elemento;
            }

            foreach (var neto in OrdemDeTab(filho))
            {
                yield return neto;
            }
        }
    }

    private static int PosicaoDoBotao(IReadOnlyList<UIElement> ordem, string conteudo)
    {
        var indice = ordem.ToList().FindIndex(e => e is Button { Content: string texto } && texto == conteudo);
        Assert.True(indice >= 0, $"Botao \"{conteudo}\" nao esta na ordem de Tab.");
        return indice;
    }

    [Fact]
    public void AtendimentoView_ExpoeCampoDeNumeroEDeBusca()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            var view = new AtendimentoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);

            var numero = Assert.IsType<TextBox>(view.FindName("CampoNumeroComanda"));
            var busca = Assert.IsType<TextBox>(view.FindName("CampoBusca"));

            viewModel.NovoNumero = "12";
            Assert.Equal("12", numero.Text);
            viewModel.TextoBuscaCatalogo = "refri";
            Assert.Equal("refri", busca.Text);

            // Enter no campo de numero executa AbrirCommand (o KeyBinding herda o DataContext)
            var enter = Assert.Single(numero.InputBindings.OfType<KeyBinding>(), k => k.Key == Key.Enter);
            Assert.Same(viewModel.AbrirCommand, enter.Command);
        });
    }

    [Fact]
    public void AtendimentoView_OrdemDeTab_BuscaCategoriasCardsItensVoltarCancelarFinalizar()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            var view = new AtendimentoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);

            var ordem = OrdemDeTab(view).ToList();

            var busca = ordem.IndexOf((UIElement)view.FindName("CampoBusca"));
            var todas = PosicaoDoBotao(ordem, "Todas");
            var categoria = PosicaoDoBotao(ordem, "Bebidas");
            var primeiroCard = ordem.FindIndex(e => e is Button { Command: not null } b && b.Width == 128);
            var menos = PosicaoDoBotao(ordem, "−");
            var voltar = PosicaoDoBotao(ordem, "Voltar");
            var cancelar = PosicaoDoBotao(ordem, "Cancelar comanda");
            var finalizar = PosicaoDoBotao(ordem, "Finalizar comanda (F4)");

            Assert.True(busca >= 0, "A busca precisa estar na ordem de Tab.");
            Assert.True(primeiroCard >= 0, "Os cards do menu precisam estar na ordem de Tab.");
            Assert.True(busca < todas, "busca antes das categorias");
            Assert.True(todas < categoria, "\"Todas\" antes das categorias");
            Assert.True(categoria < primeiroCard, "categorias antes dos cards do menu");
            Assert.True(primeiroCard < menos, "cards do menu antes dos itens da comanda");
            Assert.True(menos < voltar, "itens antes de Voltar");
            Assert.True(voltar < cancelar, "Voltar antes de Cancelar");
            Assert.True(cancelar < finalizar, "Cancelar antes de Finalizar");

            // o painel focavel (so para o foco programatico antigo) nao pode ser uma parada de Tab
            Assert.DoesNotContain(ordem, e => e is Grid { Name: "PainelComanda" });
        });
    }

    [Fact]
    public void AtendimentoView_ItemSelecionado_TemLinhaComFundoDestacado()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            var view = new AtendimentoView(viewModel);
            var destaque = Color.FromRgb(0xFB, 0xE3, 0xC2);

            viewModel.ItemSelecionadoId = null;
            CarregamentoDeXamlTests.MedirEOrganizar(view);
            Assert.DoesNotContain(Descendentes(view).OfType<Border>(), b => b.Background is SolidColorBrush s && s.Color == destaque);

            viewModel.ItemSelecionadoId = viewModel.Itens[0].Id;
            view.UpdateLayout();
            Assert.Single(Descendentes(view).OfType<Border>(), b => b.Background is SolidColorBrush s && s.Color == destaque);
        });
    }

    [Fact]
    public void ItemSelecionadoParaCorConverter_DestacaSoALinhaSelecionada()
    {
        var conversor = new ItemSelecionadoParaCorConverter();

        var fundoSelecionada = Assert.IsAssignableFrom<SolidColorBrush>(conversor.Convert(new object[] { 5, 5 }, typeof(Brush), null!, CultureInfo.InvariantCulture));
        var marcaSelecionada = Assert.IsAssignableFrom<SolidColorBrush>(conversor.Convert(new object[] { 5, 5 }, typeof(Brush), "Marca", CultureInfo.InvariantCulture));
        var outra = conversor.Convert(new object[] { 5, 6 }, typeof(Brush), null!, CultureInfo.InvariantCulture);
        var semSelecao = conversor.Convert(new object[] { 5, null! }, typeof(Brush), null!, CultureInfo.InvariantCulture);

        Assert.Equal(Color.FromRgb(0xFB, 0xE3, 0xC2), fundoSelecionada.Color);
        Assert.Equal(Color.FromRgb(0xC5, 0x68, 0x20), marcaSelecionada.Color);
        Assert.Same(Brushes.Transparent, outra);
        Assert.Same(Brushes.Transparent, semSelecao);
    }

    [Theory]
    [InlineData("7", true)]
    [InlineData("0123456789", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("a", false)]
    [InlineData("1a", false)]
    [InlineData("-1", false)]
    [InlineData("1 2", false)]
    [InlineData("٣", false)] // digito arabe-indico: char.IsDigit aceitaria, IsAsciiDigit nao
    public void SomenteDigitos_AceitaApenasDigitosAscii(string? texto, bool esperado)
    {
        Assert.Equal(esperado, AtendimentoView.SomenteDigitos(texto));
    }

    // ---- teclas: eventos roteados reais (PreviewKeyDown) contra a view, sem exibir janela ----

    private static bool Pressionar(AtendimentoView view, Key tecla, IInputElement origem)
    {
        using var fonte = new HwndSource(new HwndSourceParameters("teste-teclado"));
        var evento = new KeyEventArgs(Keyboard.PrimaryDevice, fonte, Environment.TickCount, tecla)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
            Source = origem
        };
        view.RaiseEvent(evento);
        return evento.Handled;
    }

    [Fact]
    public void Teclas_SetasESinais_OperamNoItemSelecionadoForaDeCampoDeTexto()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            var view = new AtendimentoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);
            var item = viewModel.Itens[0]; // quantidade 2
            viewModel.ItemSelecionadoId = null;

            Assert.True(Pressionar(view, Key.Down, view));
            Assert.Equal(item.Id, viewModel.ItemSelecionadoId);

            Assert.True(Pressionar(view, Key.Add, view));
            Assert.Equal(3, viewModel.Itens[0].Quantidade);
            Assert.True(Pressionar(view, Key.OemPlus, view));
            Assert.Equal(4, viewModel.Itens[0].Quantidade);
            Assert.True(Pressionar(view, Key.Subtract, view));
            Assert.True(Pressionar(view, Key.OemMinus, view));
            Assert.Equal(2, viewModel.Itens[0].Quantidade);

            Assert.True(Pressionar(view, Key.Delete, view)); // FakeConfirmador confirma
            Assert.Empty(viewModel.Itens);
            Assert.Null(viewModel.ItemSelecionadoId);
        });
    }

    [Theory]
    [InlineData(Key.Add)]
    [InlineData(Key.OemPlus)]
    [InlineData(Key.Subtract)]
    [InlineData(Key.OemMinus)]
    [InlineData(Key.Delete)]
    [InlineData(Key.Down)]
    [InlineData(Key.Up)]
    public void Teclas_ComFocoNaBusca_NaoAgemSobreOsItens(Key tecla)
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            var view = new AtendimentoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);
            var busca = (TextBox)view.FindName("CampoBusca");
            viewModel.ItemSelecionadoId = viewModel.Itens[0].Id;

            var tratada = Pressionar(view, tecla, busca);

            Assert.False(tratada);
            Assert.Single(viewModel.Itens);
            Assert.Equal(2, viewModel.Itens[0].Quantidade);
            Assert.Equal(viewModel.Itens[0].Id, viewModel.ItemSelecionadoId);
        });
    }

    [Theory]
    [InlineData(Key.Add)]
    [InlineData(Key.OemPlus)]
    [InlineData(Key.Subtract)]
    [InlineData(Key.OemMinus)]
    [InlineData(Key.Delete)]
    [InlineData(Key.Down)]
    [InlineData(Key.Up)]
    public void Teclas_ComFocoNoMenu_NaoAgemSobreOsItens(Key tecla)
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            var view = new AtendimentoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);
            var menu = (DependencyObject)view.FindName("PainelMenu");
            var botaoDoMenu = Descendentes(menu).OfType<Button>().First();
            viewModel.ItemSelecionadoId = viewModel.Itens[0].Id;

            var tratada = Pressionar(view, tecla, botaoDoMenu);

            Assert.False(tratada);
            Assert.Single(viewModel.Itens);
            Assert.Equal(2, viewModel.Itens[0].Quantidade);
            Assert.Equal(viewModel.Itens[0].Id, viewModel.ItemSelecionadoId);
        });
    }

    [Fact]
    public void AtendimentoView_SemComandaSelecionada_InstanciaOsSlotsComFocoVisivel()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            viewModel.FecharEdicaoCommand.Execute(null);
            var view = new AtendimentoView(viewModel);

            CarregamentoDeXamlTests.MedirEOrganizar(view);

            var grade = (DependencyObject)view.FindName("PainelGrade");
            var slots = Descendentes(grade).OfType<Button>()
                .Where(b => ReferenceEquals(b.Command, viewModel.AbrirOuSelecionarSlotCommand))
                .ToList();
            Assert.NotEmpty(slots);
            var foco = System.Windows.Application.Current.FindResource("FocoVisivel");
            Assert.All(slots, b => Assert.Same(foco, b.FocusVisualStyle));
        });
    }

    [Fact]
    public void Enter_NaBusca_AdicionaOPrimeiroEMantemOTexto()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            var view = new AtendimentoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);
            var busca = (TextBox)view.FindName("CampoBusca");
            viewModel.TextoBuscaCatalogo = "refri";

            Assert.True(Pressionar(view, Key.Enter, busca));

            Assert.Equal(3, viewModel.Itens.Single().Quantidade);
            Assert.Equal("refri", busca.Text);
            Assert.Equal(0, busca.SelectionStart);
            Assert.Equal(5, busca.SelectionLength);
        });
    }

    [Fact]
    public void Enter_NaBuscaVazia_NaoAdicionaNada()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            var view = new AtendimentoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);
            var busca = (TextBox)view.FindName("CampoBusca");
            viewModel.TextoBuscaCatalogo = string.Empty;

            Pressionar(view, Key.Enter, busca);

            Assert.Equal(2, viewModel.Itens.Single().Quantidade);
        });
    }

    [Fact]
    public void Enter_NaBuscaSemResultado_MostraMensagem()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            var view = new AtendimentoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);
            var busca = (TextBox)view.FindName("CampoBusca");
            viewModel.TextoBuscaCatalogo = "zzz";

            Pressionar(view, Key.Enter, busca);

            Assert.Equal("Nenhum produto encontrado.", viewModel.Mensagem);
        });
    }

    [Fact]
    public void Esc_ComTextoNaBusca_LimpaSemSair_ESemTextoVolta()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            var view = new AtendimentoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);
            var busca = (TextBox)view.FindName("CampoBusca");
            viewModel.TextoBuscaCatalogo = "refri";

            Assert.True(Pressionar(view, Key.Escape, busca));
            Assert.Equal(string.Empty, viewModel.TextoBuscaCatalogo);
            Assert.NotNull(viewModel.ComandaAtual);

            Assert.True(Pressionar(view, Key.Escape, busca));
            Assert.Null(viewModel.ComandaAtual);
        });
    }

    [Fact]
    public void Esc_ForaDaBusca_VoltaMesmoComTextoNaBusca()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var (viewModel, _) = CarregamentoDeXamlTests.CriarAtendimento();
            var view = new AtendimentoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);
            viewModel.TextoBuscaCatalogo = "refri";

            Assert.True(Pressionar(view, Key.Escape, view));

            Assert.Null(viewModel.ComandaAtual);
        });
    }
}
