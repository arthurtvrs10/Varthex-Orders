using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using VarthexComanda.Desktop.Atendimento;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Atendimento;

/// <summary>Operacao do atendimento por teclado (RNF18 / CT17): busca + Enter, selecao de item e +/-/Delete.</summary>
[Trait("Requisito", "RNF18")]
[Trait("Caso", "CT17")]
public class AtendimentoViewModelTecladoTests
{
    private static (AtendimentoViewModel ViewModel, FakeConfirmador Confirmador) CriarViewModel()
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var bebidas = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var lanches = new CadastrarCategoria(categorias, relogio).Executar("Lanches").Valor!;
        var produtos = new FakeProdutoRepository();
        var cadastrar = new CadastrarProduto(produtos, categorias, relogio);
        cadastrar.Executar("Refrigerante", bebidas.Id, 500);
        cadastrar.Executar("Suco", bebidas.Id, 700);
        cadastrar.Executar("Pastel", lanches.Id, 900);
        var comandas = new FakeComandaRepository();
        var confirmador = new FakeConfirmador { ProximaResposta = true };

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
            new FakeEncerramentoDialog(comandas, relogio),
            new ObterConfiguracao(new FakeConfiguracaoRepository()),
            relogio);

        return (viewModel, confirmador);
    }

    private static AtendimentoViewModel ComandaAbertaComTresItens(out FakeConfirmador confirmador)
    {
        var (viewModel, conf) = CriarViewModel();
        confirmador = conf;
        viewModel.NovoNumero = "7";
        viewModel.AbrirCommand.Execute(null);
        foreach (var produto in viewModel.ProdutosCatalogo.ToList())
        {
            viewModel.AdicionarProdutoAoItemCommand.Execute(produto);
        }
        return viewModel;
    }

    // ---- (d) fluxo por numero ----

    [Fact]
    public void Abrir_ComNovoNumero7_AbreComandaEZeraOCampo()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "7";

        viewModel.AbrirCommand.Execute(null);

        Assert.Equal(7, viewModel.ComandaAtual!.Numero);
        Assert.Equal(string.Empty, viewModel.NovoNumero);
        Assert.Null(viewModel.ItemSelecionadoId);
    }

    // ---- (a) busca + Enter ----

    [Fact]
    public void AdicionarPrimeiroDaBusca_AdicionaOPrimeiroDaListaEMantemOTexto()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "7";
        viewModel.AbrirCommand.Execute(null);
        viewModel.TextoBuscaCatalogo = "s";

        viewModel.AdicionarPrimeiroDaBuscaCommand.Execute(null);

        var item = Assert.Single(viewModel.Itens);
        Assert.Equal(viewModel.ProdutosCatalogo[0].Id, item.ProdutoId);
        Assert.Equal("s", viewModel.TextoBuscaCatalogo);
        Assert.Equal(item.Id, viewModel.ItemSelecionadoId);
    }

    [Fact]
    public void AdicionarPrimeiroDaBusca_RespeitaCategoriaEFiltro()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "7";
        viewModel.AbrirCommand.Execute(null);
        viewModel.CategoriaCatalogo = viewModel.Categorias.Single(c => c.Nome == "Lanches");

        viewModel.AdicionarPrimeiroDaBuscaCommand.Execute(null);

        Assert.Equal("Pastel", Assert.Single(viewModel.Itens).NomeProduto);

        viewModel.LimparFiltroCommand.Execute(null);
        viewModel.TextoBuscaCatalogo = "suc";
        viewModel.AdicionarPrimeiroDaBuscaCommand.Execute(null);

        Assert.Equal(new[] { "Pastel", "Suco" }, viewModel.Itens.Select(i => i.NomeProduto).OrderBy(n => n));
    }

    [Fact]
    public void AdicionarPrimeiroDaBusca_MesmoProdutoDuasVezes_IncrementaQuantidade()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "7";
        viewModel.AbrirCommand.Execute(null);
        viewModel.TextoBuscaCatalogo = "refri";

        viewModel.AdicionarPrimeiroDaBuscaCommand.Execute(null);
        viewModel.AdicionarPrimeiroDaBuscaCommand.Execute(null);

        Assert.Equal(2, Assert.Single(viewModel.Itens).Quantidade);
    }

    [Fact]
    public void AdicionarPrimeiroDaBusca_SemResultado_MostraMensagemENaoAdiciona()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "7";
        viewModel.AbrirCommand.Execute(null);
        viewModel.TextoBuscaCatalogo = "xyz";

        viewModel.AdicionarPrimeiroDaBuscaCommand.Execute(null);

        Assert.Equal("Nenhum produto encontrado.", viewModel.Mensagem);
        Assert.Empty(viewModel.Itens);
        Assert.Null(viewModel.ItemSelecionadoId);
    }

    // ---- (b) selecao ----

    [Fact]
    public void ItemAdicionado_PassaAserOSelecionado()
    {
        var viewModel = ComandaAbertaComTresItens(out _);

        var ultimo = viewModel.Itens.Single(i => i.ProdutoId == viewModel.ProdutosCatalogo.Last().Id);
        Assert.Equal(ultimo.Id, viewModel.ItemSelecionadoId);
    }

    [Fact]
    public void SelecionarProximoEAnterior_PercorremSemPassarDosLimites()
    {
        var viewModel = ComandaAbertaComTresItens(out _);
        var ids = viewModel.Itens.Select(i => i.Id).ToList();

        viewModel.ItemSelecionadoId = null;
        viewModel.SelecionarProximoItemCommand.Execute(null);
        Assert.Equal(ids[0], viewModel.ItemSelecionadoId);
        viewModel.SelecionarProximoItemCommand.Execute(null);
        viewModel.SelecionarProximoItemCommand.Execute(null);
        Assert.Equal(ids[2], viewModel.ItemSelecionadoId);
        viewModel.SelecionarProximoItemCommand.Execute(null);
        Assert.Equal(ids[2], viewModel.ItemSelecionadoId);

        viewModel.SelecionarItemAnteriorCommand.Execute(null);
        Assert.Equal(ids[1], viewModel.ItemSelecionadoId);
        viewModel.SelecionarItemAnteriorCommand.Execute(null);
        viewModel.SelecionarItemAnteriorCommand.Execute(null);
        Assert.Equal(ids[0], viewModel.ItemSelecionadoId);
    }

    [Fact]
    public void SelecionarItemAnterior_SemSelecao_VaiParaOUltimo()
    {
        var viewModel = ComandaAbertaComTresItens(out _);
        viewModel.ItemSelecionadoId = null;

        viewModel.SelecionarItemAnteriorCommand.Execute(null);

        Assert.Equal(viewModel.Itens[^1].Id, viewModel.ItemSelecionadoId);
    }

    [Fact]
    public void Selecionar_SemItens_NaoFazNadaENaoLanca()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "7";
        viewModel.AbrirCommand.Execute(null);

        viewModel.SelecionarProximoItemCommand.Execute(null);
        viewModel.SelecionarItemAnteriorCommand.Execute(null);

        Assert.Null(viewModel.ItemSelecionadoId);
    }

    // ---- (c) operar no selecionado ----

    [Fact]
    public void AumentarSelecionado_AumentaSoOItemSelecionado()
    {
        var viewModel = ComandaAbertaComTresItens(out _);
        var alvo = viewModel.Itens[1];
        viewModel.ItemSelecionadoId = alvo.Id;

        viewModel.AumentarSelecionadoCommand.Execute(null);

        Assert.Equal(2, viewModel.Itens.Single(i => i.Id == alvo.Id).Quantidade);
        Assert.Equal(2, viewModel.Itens.Count(i => i.Quantidade == 1));
        Assert.Equal(alvo.Id, viewModel.ItemSelecionadoId);
    }

    [Fact]
    public void DiminuirSelecionado_ComQuantidadeMaiorQueUm_Diminui()
    {
        var viewModel = ComandaAbertaComTresItens(out _);
        var alvo = viewModel.Itens[0];
        viewModel.ItemSelecionadoId = alvo.Id;
        viewModel.AumentarSelecionadoCommand.Execute(null);

        viewModel.DiminuirSelecionadoCommand.Execute(null);

        Assert.Equal(1, viewModel.Itens.Single(i => i.Id == alvo.Id).Quantidade);
        Assert.Equal(3, viewModel.Itens.Count);
    }

    [Fact]
    public void DiminuirSelecionado_ComQuantidadeUm_ConfirmaERemove()
    {
        var viewModel = ComandaAbertaComTresItens(out var confirmador);
        confirmador.ProximaResposta = true;
        viewModel.ItemSelecionadoId = viewModel.Itens[1].Id;

        viewModel.DiminuirSelecionadoCommand.Execute(null);

        Assert.Equal(2, viewModel.Itens.Count);
    }

    [Fact]
    public void RemoverSelecionado_ConfirmadorRecusa_MantemItemESelecao()
    {
        var viewModel = ComandaAbertaComTresItens(out var confirmador);
        confirmador.ProximaResposta = false;
        var alvo = viewModel.Itens[1].Id;
        viewModel.ItemSelecionadoId = alvo;

        viewModel.RemoverSelecionadoCommand.Execute(null);

        Assert.Equal(3, viewModel.Itens.Count);
        Assert.Equal(alvo, viewModel.ItemSelecionadoId);
    }

    [Fact]
    public void RemoverSelecionado_NoMeio_SelecionaOVizinho()
    {
        var viewModel = ComandaAbertaComTresItens(out _);
        var ids = viewModel.Itens.Select(i => i.Id).ToList();
        viewModel.ItemSelecionadoId = ids[1];

        viewModel.RemoverSelecionadoCommand.Execute(null);

        Assert.Equal(new[] { ids[0], ids[2] }, viewModel.Itens.Select(i => i.Id));
        Assert.Equal(ids[2], viewModel.ItemSelecionadoId);
    }

    [Fact]
    public void RemoverSelecionado_NoFim_SelecionaOAnterior()
    {
        var viewModel = ComandaAbertaComTresItens(out _);
        var ids = viewModel.Itens.Select(i => i.Id).ToList();
        viewModel.ItemSelecionadoId = ids[2];

        viewModel.RemoverSelecionadoCommand.Execute(null);

        Assert.Equal(ids[1], viewModel.ItemSelecionadoId);
    }

    [Fact]
    public void RemoverSelecionado_UltimoItem_SelecaoFicaNula()
    {
        var (viewModel, _) = CriarViewModel();
        viewModel.NovoNumero = "7";
        viewModel.AbrirCommand.Execute(null);
        viewModel.TextoBuscaCatalogo = "refri";
        viewModel.AdicionarPrimeiroDaBuscaCommand.Execute(null);

        viewModel.RemoverSelecionadoCommand.Execute(null);

        Assert.Empty(viewModel.Itens);
        Assert.Null(viewModel.ItemSelecionadoId);
    }

    [Fact]
    public void AcoesNoSelecionado_SemSelecao_NaoFazemNadaENaoLancam()
    {
        var viewModel = ComandaAbertaComTresItens(out _);
        viewModel.ItemSelecionadoId = null;
        var antes = viewModel.Itens.Select(i => (i.Id, i.Quantidade)).ToList();

        viewModel.AumentarSelecionadoCommand.Execute(null);
        viewModel.DiminuirSelecionadoCommand.Execute(null);
        viewModel.RemoverSelecionadoCommand.Execute(null);

        Assert.Equal(antes, viewModel.Itens.Select(i => (i.Id, i.Quantidade)));
        Assert.Null(viewModel.ItemSelecionadoId);
    }

    [Fact]
    public void FecharEdicao_LimpaSelecaoEBusca()
    {
        var viewModel = ComandaAbertaComTresItens(out _);
        viewModel.TextoBuscaCatalogo = "suc";

        viewModel.FecharEdicaoCommand.Execute(null);

        Assert.Null(viewModel.ItemSelecionadoId);
        Assert.Equal(string.Empty, viewModel.TextoBuscaCatalogo);
    }
}
