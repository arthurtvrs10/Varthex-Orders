using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using VarthexComanda.Desktop.Atendimento;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class AtendimentoViewModelTests
{
    [Fact]
    public void SalvarNomeAtualizaGradeERecarregaAoSelecionar()
    {
        var (vm, _, _, _) = CriarViewModel();
        vm.NovoNumero = "1";
        vm.AbrirCommand.Execute(null);
        vm.NomeCliente = "  Maria  ";
        vm.SalvarNomeClienteCommand.Execute(null);
        Assert.Equal("Maria", vm.Slots.Single(s => s.Numero == 1).NomeCliente);
        vm.FecharEdicaoCommand.Execute(null);
        vm.AbrirOuSelecionarSlotCommand.Execute(vm.Slots.Single(s => s.Numero == 1));
        Assert.Equal("Maria", vm.NomeCliente);
        vm.NomeCliente = "";
        vm.SalvarNomeClienteCommand.Execute(null);
        Assert.Null(vm.ComandaAtual!.NomeCliente);
    }

    private static (AtendimentoViewModel viewModel, FakeProdutoRepository produtos, FakeConfirmador confirmador, FakeEncerramentoDialog encerramentoDialog) CriarViewModel(bool confirmar = true)
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500);
        var comandas = new FakeComandaRepository();
        var confirmador = new FakeConfirmador { ProximaResposta = confirmar };
        var encerramentoDialog = new FakeEncerramentoDialog(comandas, relogio);

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
            new AdicionarItem(comandas, produtos, relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(produtos),
            confirmador,
            encerramentoDialog,
            new ObterConfiguracao(new FakeConfiguracaoRepository()),
            relogio);

        return (viewModel, produtos, confirmador, encerramentoDialog);
    }

    [Fact]
    [Trait("Caso", "CT01")]
    public void Abrir_NumeroValido_CriaComandaEMostraNaGrade()
    {
        var (viewModel, _, _, _) = CriarViewModel();
        viewModel.NovoNumero = "10";

        viewModel.AbrirCommand.Execute(null);

        Assert.Single(viewModel.ComandasAbertas);
        Assert.NotNull(viewModel.ComandaAtual);
        Assert.Equal(10, viewModel.ComandaAtual!.Numero);
    }

    [Fact]
    [Trait("Caso", "CT02")]
    public void Abrir_MesmoNumeroDuasVezes_SegundaSelecionaAComandaSemDuplicarNaGrade()
    {
        var (viewModel, _, _, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.Mensagem);
        Assert.Equal(string.Empty, viewModel.NovoNumero);
        Assert.Equal(10, viewModel.ComandaAtual!.Numero);
        Assert.Single(viewModel.ComandasAbertas);
    }

    [Fact]
    [Trait("Caso", "CT03")]
    public void AdicionarProdutoDuasVezes_IncrementaQuantidadeEmVezDeDuplicar()
    {
        var (viewModel, produtos, _, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];

        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        Assert.Single(viewModel.Itens);
        Assert.Equal(2, viewModel.Itens[0].Quantidade);
        Assert.Equal(1000, viewModel.ComandaAtual!.TotalCentavos);
    }

    [Fact]
    public void DiminuirQuantidadeAteZero_ConfirmadorAceita_RemoveItem()
    {
        var (viewModel, produtos, confirmador, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        confirmador.ProximaResposta = true;
        viewModel.DiminuirQuantidadeCommand.Execute(viewModel.Itens[0]);

        Assert.Empty(viewModel.Itens);
        Assert.Equal(0, viewModel.ComandaAtual!.TotalCentavos);
    }

    [Fact]
    public void DiminuirQuantidadeAteZero_ConfirmadorRecusa_ItemPermanece()
    {
        var (viewModel, produtos, confirmador, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        confirmador.ProximaResposta = false;
        viewModel.DiminuirQuantidadeCommand.Execute(viewModel.Itens[0]);

        Assert.Single(viewModel.Itens);
        Assert.Equal(1, viewModel.Itens[0].Quantidade);
        Assert.Equal(500, viewModel.ComandaAtual!.TotalCentavos);
    }

    [Fact]
    public void Remover_ConfirmadorRecusa_ItemPermanece()
    {
        var (viewModel, produtos, confirmador, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        confirmador.ProximaResposta = false;
        viewModel.RemoverCommand.Execute(viewModel.Itens[0]);

        Assert.Single(viewModel.Itens);
    }

    [Fact]
    public void CancelarComandaAtual_LiberaNumeroNaGrade()
    {
        var (viewModel, _, _, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        viewModel.CancelarComandaAtualCommand.Execute(null);

        Assert.Empty(viewModel.ComandasAbertas);
        Assert.Null(viewModel.ComandaAtual);
    }

    [Fact]
    public void CancelarComandaAtual_ConfirmadorRecusa_ComandaPermaneceAberta()
    {
        var (viewModel, produtos, confirmador, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        confirmador.ProximaResposta = false;
        viewModel.CancelarComandaAtualCommand.Execute(null);

        Assert.NotNull(viewModel.ComandaAtual);
        Assert.Single(viewModel.ComandasAbertas);
    }

    [Fact]
    [Trait("Caso", "CT07")]
    public void VerTotal_ComandaSemItens_ComandoDesabilitado()
    {
        var (viewModel, _, _, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        Assert.False(viewModel.VerTotalCommand.CanExecute(null));
    }

    [Fact]
    public void VerTotal_DialogoConfirma_ComandaSaiDaGrade()
    {
        var (viewModel, produtos, _, encerramentoDialog) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);
        var comandaId = viewModel.ComandaAtual!.Id;
        encerramentoDialog.ProximaResposta = true;

        viewModel.VerTotalCommand.Execute(null);

        Assert.Equal(comandaId, encerramentoDialog.ComandaIdRecebida);
        Assert.Null(viewModel.ComandaAtual);
        Assert.Empty(viewModel.ComandasAbertas);
    }

    [Fact]
    [Trait("Caso", "CT09")]
    public void VerTotal_DialogoCancela_ComandaPermaneceAberta()
    {
        var (viewModel, produtos, _, encerramentoDialog) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);
        encerramentoDialog.ProximaResposta = false;

        viewModel.VerTotalCommand.Execute(null);

        Assert.NotNull(viewModel.ComandaAtual);
        Assert.Single(viewModel.ComandasAbertas);
    }

    [Fact]
    public void AtualizarSlots_SemConfiguracao_Cria20SlotsComOsAbertosCorretos()
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var comandas = new FakeComandaRepository();
        var confirmador = new FakeConfirmador { ProximaResposta = true };
        var encerramentoDialog = new FakeEncerramentoDialog(comandas, relogio);

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
            new AdicionarItem(comandas, new FakeProdutoRepository(), relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(new FakeProdutoRepository()),
            confirmador,
            encerramentoDialog,
            new ObterConfiguracao(new FakeConfiguracaoRepository()),
            relogio);

        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);

        Assert.Equal(20, viewModel.Slots.Count);
        var slotAberto = viewModel.Slots.Single(s => s.Numero == 10);
        Assert.True(slotAberto.Aberta);
        Assert.NotNull(slotAberto.ComandaId);
        var slotLivre = viewModel.Slots.Single(s => s.Numero == 1);
        Assert.False(slotLivre.Aberta);
        Assert.Null(slotLivre.ComandaId);
    }

    [Fact]
    public void AtualizarSlots_ComConfiguracao_UsaTamanhoConfigurado()
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var comandas = new FakeComandaRepository();
        var confirmador = new FakeConfirmador { ProximaResposta = true };
        var encerramentoDialog = new FakeEncerramentoDialog(comandas, relogio);
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("comandas.quantidade_maxima", "5", DateTime.UtcNow);

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
            new AdicionarItem(comandas, new FakeProdutoRepository(), relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(new FakeProdutoRepository()),
            confirmador,
            encerramentoDialog,
            new ObterConfiguracao(configuracoes),
            relogio);

        Assert.Equal(5, viewModel.Slots.Count);
    }

    [Fact]
    public void AbrirOuSelecionarSlot_SlotLivre_AbreComandaNesseNumero()
    {
        var (viewModel, _, _, _) = CriarViewModel();

        var slotLivre = viewModel.Slots.Single(s => s.Numero == 7);
        viewModel.AbrirOuSelecionarSlotCommand.Execute(slotLivre);

        Assert.NotNull(viewModel.ComandaAtual);
        Assert.Equal(7, viewModel.ComandaAtual!.Numero);
    }

    [Fact]
    public void AbrirOuSelecionarSlot_SlotAberto_EntraNaEdicao()
    {
        var (viewModel, _, _, _) = CriarViewModel();
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        viewModel.FecharEdicaoCommand.Execute(null);

        var slotAberto = viewModel.Slots.Single(s => s.Numero == 10);
        viewModel.AbrirOuSelecionarSlotCommand.Execute(slotAberto);

        Assert.NotNull(viewModel.ComandaAtual);
        Assert.Equal(10, viewModel.ComandaAtual!.Numero);
    }

    [Fact]
    [Trait("Caso", "CT08")]
    public void AtualizarSlots_ComandaAbertaMostraTotalETempoFormatados()
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500);
        var comandas = new FakeComandaRepository();
        var confirmador = new FakeConfirmador { ProximaResposta = true };
        var encerramentoDialog = new FakeEncerramentoDialog(comandas, relogio);

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
            new AdicionarItem(comandas, produtos, relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(produtos),
            confirmador,
            encerramentoDialog,
            new ObterConfiguracao(new FakeConfiguracaoRepository()),
            relogio);

        viewModel.NovoNumero = "7";
        viewModel.AbrirCommand.Execute(null);

        relogio.UtcNow = relogio.UtcNow.AddMinutes(5);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        var slot = viewModel.Slots.Single(s => s.Numero == 7);
        Assert.True(slot.Aberta);
        Assert.Equal("R$ 5,00", slot.TotalFormatado);
        Assert.Equal("há 5 min", slot.TempoFormatado);
    }

    [Fact]
    public void AtualizarSlots_ComandaAbertaComNumeroForaDoTamanhoConfigurado_ContinuaVisivelEAcessivel()
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var comandas = new FakeComandaRepository();
        var confirmador = new FakeConfirmador { ProximaResposta = true };
        var encerramentoDialog = new FakeEncerramentoDialog(comandas, relogio);
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("comandas.quantidade_maxima", "5", DateTime.UtcNow);

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
            new AdicionarItem(comandas, new FakeProdutoRepository(), relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(new FakeProdutoRepository()),
            confirmador,
            encerramentoDialog,
            new ObterConfiguracao(configuracoes),
            relogio);

        viewModel.NovoNumero = "30";
        viewModel.AbrirCommand.Execute(null);

        Assert.Equal(6, viewModel.Slots.Count);
        var slotForaDaGrade = viewModel.Slots.Single(s => s.Numero == 30);
        Assert.True(slotForaDaGrade.Aberta);
        Assert.NotNull(slotForaDaGrade.ComandaId);

        viewModel.AbrirOuSelecionarSlotCommand.Execute(slotForaDaGrade);

        Assert.NotNull(viewModel.ComandaAtual);
        Assert.Equal(30, viewModel.ComandaAtual!.Numero);
    }

    [Fact]
    public void AtualizarSlots_ComandaAbertaAgoraMesmo_MostraTempoAgora()
    {
        var (viewModel, _, _, _) = CriarViewModel();

        viewModel.NovoNumero = "7";
        viewModel.AbrirCommand.Execute(null);

        var slot = viewModel.Slots.Single(s => s.Numero == 7);
        Assert.Equal("agora", slot.TempoFormatado);
    }

    [Fact]
    public void AtualizarSlots_ComandaAbertaHaMaisDeUmaHora_MostraTempoEmHoras()
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var comandas = new FakeComandaRepository();
        var confirmador = new FakeConfirmador { ProximaResposta = true };
        var encerramentoDialog = new FakeEncerramentoDialog(comandas, relogio);

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(new FakeConfiguracaoRepository())),
            new AdicionarItem(comandas, new FakeProdutoRepository(), relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(new FakeProdutoRepository()),
            confirmador,
            encerramentoDialog,
            new ObterConfiguracao(new FakeConfiguracaoRepository()),
            relogio);

        viewModel.NovoNumero = "7";
        viewModel.AbrirCommand.Execute(null);

        relogio.UtcNow = relogio.UtcNow.AddMinutes(90);
        viewModel.AtualizarComandasAbertas();

        var slot = viewModel.Slots.Single(s => s.Numero == 7);
        Assert.Equal("há 1 h", slot.TempoFormatado);
    }
}
