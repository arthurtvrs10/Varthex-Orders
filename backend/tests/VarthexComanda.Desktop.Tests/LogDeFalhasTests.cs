using Serilog.Events;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Backup;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using VarthexComanda.Application.Tests.Suporte;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Desktop.Catalogo;
using VarthexComanda.Desktop.Configuracao;
using VarthexComanda.Desktop.Tests.Atendimento;
using VarthexComanda.Desktop.Tests.Backup;
using VarthexComanda.Desktop.Tests.Suporte;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Desktop.Tests;

// RNF16, RF26: falhas tecnicas dos ViewModels vao para o log local, so com a operacao e a excecao,
// e a mensagem amigavel ao usuario continua exatamente a mesma.
public class LogDeFalhasTests
{
    // dados de negocio usados nos cenarios: nunca podem aparecer no log
    private static readonly string[] DadosDeNegocio = { "Refrigerante", "Hamburguer", "5,00", "R$" };

    // ---------- Atendimento ----------

    private sealed class CenarioAtendimento
    {
        public required AtendimentoViewModel ViewModel { get; init; }
        public required ComandaRepositoryQueFalha Comandas { get; init; }
        public required Produto Produto { get; init; }
    }

    private static CenarioAtendimento CriarAtendimento(ColetorDeLog? coletor)
    {
        var categorias = new FakeCategoriaRepository();
        var relogio = new FakeClock();
        var categoria = new CadastrarCategoria(categorias, relogio).Executar("Bebidas").Valor!;
        var produtos = new FakeProdutoRepository();
        new CadastrarProduto(produtos, categorias, relogio).Executar("Refrigerante", categoria.Id, 500);
        var comandas = new ComandaRepositoryQueFalha(new FakeComandaRepository());
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
            relogio,
            coletor?.Logger);

        // comanda aberta com 2 unidades do produto, ja com a falha desligada
        viewModel.NovoNumero = "10";
        viewModel.AbrirCommand.Execute(null);
        var produto = produtos.Pesquisar(null, null)[0];
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);
        viewModel.AdicionarProdutoAoItemCommand.Execute(produto);

        return new CenarioAtendimento { ViewModel = viewModel, Comandas = comandas, Produto = produto };
    }

    public static IEnumerable<object[]> OperacoesDeAtendimento() => new[]
    {
        new object[] { "AbrirComanda", "Não foi possível abrir a comanda. Tente novamente." },
        new object[] { "AdicionarItem", "Não foi possível adicionar o produto à comanda. Tente novamente." },
        new object[] { "AumentarQuantidade", "Não foi possível atualizar a quantidade do item. Tente novamente." },
        new object[] { "DiminuirQuantidade", "Não foi possível atualizar a quantidade do item. Tente novamente." },
        new object[] { "RemoverItem", "Não foi possível remover o item. Tente novamente." },
        new object[] { "CancelarComanda", "Não foi possível cancelar a comanda. Tente novamente." },
    };

    private static void ExecutarOperacao(AtendimentoViewModel vm, Produto produto, string operacao)
    {
        switch (operacao)
        {
            case "AbrirComanda":
                vm.NovoNumero = "77";
                vm.AbrirCommand.Execute(null);
                break;
            case "AdicionarItem":
                vm.AdicionarProdutoAoItemCommand.Execute(produto);
                break;
            case "AumentarQuantidade":
                vm.AumentarQuantidadeCommand.Execute(vm.Itens[0]);
                break;
            case "DiminuirQuantidade":
                vm.DiminuirQuantidadeCommand.Execute(vm.Itens[0]);
                break;
            case "RemoverItem":
                vm.RemoverCommand.Execute(vm.Itens[0]);
                break;
            case "CancelarComanda":
                vm.CancelarComandaAtualCommand.Execute(null);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operacao));
        }
    }

    [Theory]
    [MemberData(nameof(OperacoesDeAtendimento))]
    public void Atendimento_FalhaTecnica_RegistraErroSoComOperacaoEMantemMensagem(string operacao, string mensagemEsperada)
    {
        var coletor = new ColetorDeLog();
        var cenario = CriarAtendimento(coletor);
        cenario.Comandas.Falhar = true;

        ExecutarOperacao(cenario.ViewModel, cenario.Produto, operacao);

        Assert.Equal(mensagemEsperada, cenario.ViewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, operacao, DadosDeNegocio);
    }

    [Theory]
    [MemberData(nameof(OperacoesDeAtendimento))]
    public void Atendimento_FalhaTecnicaSemLogger_MantemMensagemENaoLanca(string operacao, string mensagemEsperada)
    {
        var cenario = CriarAtendimento(null);
        cenario.Comandas.Falhar = true;

        var excecao = Record.Exception(() => ExecutarOperacao(cenario.ViewModel, cenario.Produto, operacao));

        Assert.Null(excecao);
        Assert.Equal(mensagemEsperada, cenario.ViewModel.Mensagem);
    }

    [Fact]
    public void Atendimento_FalhaDeDominio_NaoRegistraNoLog()
    {
        var coletor = new ColetorDeLog();
        var cenario = CriarAtendimento(coletor);

        // numero invalido para o caso de uso: Resultado.Falha, nao excecao
        // (numero de comanda ja aberta agora seleciona a comanda, sem passar pelo caso de uso)
        cenario.ViewModel.NovoNumero = "0";
        cenario.ViewModel.AbrirCommand.Execute(null);

        Assert.Equal("Informe um número de comanda válido.", cenario.ViewModel.Mensagem);
        Assert.Empty(coletor.Eventos);
    }

    // ---------- Encerramento ----------

    private static (EncerramentoViewModel ViewModel, ComandaRepositoryQueFalha Comandas) CriarEncerramento(ColetorDeLog? coletor)
    {
        var relogio = new FakeClock();
        var comandas = new ComandaRepositoryQueFalha(new FakeComandaRepository());
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        var produto = new Produto { Id = 1, CategoriaId = 1, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
        comandas.AdicionarItem(comanda.Id, produto, 2, relogio.UtcNow);

        var viewModel = new EncerramentoViewModel(new EncerrarComanda(comandas, relogio), comandas, coletor?.Logger);
        viewModel.Carregar(comanda.Id);
        viewModel.CobrancaAprovada = true;
        return (viewModel, comandas);
    }

    [Fact]
    public void Encerramento_FalhaTecnica_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var (viewModel, comandas) = CriarEncerramento(coletor);
        comandas.Falhar = true;

        viewModel.ConfirmarEncerrarCommand.Execute(null);

        Assert.Equal("A venda não foi registrada e a comanda continua aberta.", viewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "EncerrarComanda", DadosDeNegocio);
    }

    [Fact]
    public void Encerramento_FalhaTecnicaSemLogger_MantemMensagemENaoLanca()
    {
        var (viewModel, comandas) = CriarEncerramento(null);
        comandas.Falhar = true;

        var excecao = Record.Exception(() => viewModel.ConfirmarEncerrarCommand.Execute(null));

        Assert.Null(excecao);
        Assert.Equal("A venda não foi registrada e a comanda continua aberta.", viewModel.Mensagem);
    }

    // ---------- Historico ----------

    private static Venda CriarVenda() => new()
    {
        Id = 1,
        ComandaId = 1,
        Numero = 1,
        TotalCentavos = 1000,
        FinalizadaEm = new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc),
        Status = StatusVenda.Concluida
    };

    [Fact]
    public void Historico_FalhaAoCarregarVendas_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository { LancarExcecao = true };

        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio, coletor.Logger);

        Assert.Equal("Não foi possível carregar as vendas. Tente novamente.", viewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "CarregarVendas", DadosDeNegocio);
    }

    [Fact]
    public void Historico_FalhaAoCarregarItensDaVenda_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        vendas.AdicionarVenda(CriarVenda(), 10, new List<ItemComanda>());
        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio, coletor.Logger);
        var carregada = viewModel.Vendas[0];
        vendas.LancarExcecao = true;

        viewModel.VendaSelecionada = carregada;

        Assert.Equal("Não foi possível carregar os itens da venda. Tente novamente.", viewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "CarregarItensDaVenda", DadosDeNegocio);
    }

    [Fact]
    public void Historico_FalhaTecnicaSemLogger_MantemMensagemENaoLanca()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository { LancarExcecao = true };

        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        Assert.Equal("Não foi possível carregar as vendas. Tente novamente.", viewModel.Mensagem);
    }

    // ---------- Backup ----------

    private static BackupViewModel CriarBackupViewModel(FakeBackupService backupService, ColetorDeLog? coletor) =>
        new(
            new CriarBackupManual(backupService),
            new RestaurarBackup(backupService),
            new ListarBackupsRecentes(new FakeBackupRegistroRepository()),
            new FakeConfirmadorDeBackup { ProximaResposta = true },
            coletor?.Logger);

    [Fact]
    public void Backup_FalhaTecnicaAoCriar_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var viewModel = CriarBackupViewModel(new FakeBackupService { LancarExcecaoAoCriar = true }, coletor);

        viewModel.CriarBackupCommand.Execute(null);

        Assert.Equal("Não foi possível criar o backup. Tente novamente.", viewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "CriarBackup", DadosDeNegocio);
    }

    [Fact]
    public void Backup_FalhaTecnicaAoRestaurar_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var viewModel = CriarBackupViewModel(new FakeBackupService { LancarExcecaoAoRestaurar = true }, coletor);

        viewModel.RestaurarArquivoExterno("C:\\qualquer\\backup.db");

        Assert.Equal("Não foi possível restaurar o backup. Tente novamente.", viewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "RestaurarBackup", DadosDeNegocio);
    }

    [Fact]
    public void Backup_FalhaTecnicaSemLogger_MantemMensagemENaoLanca()
    {
        var viewModel = CriarBackupViewModel(new FakeBackupService { LancarExcecaoAoCriar = true }, null);

        var excecao = Record.Exception(() => viewModel.CriarBackupCommand.Execute(null));

        Assert.Null(excecao);
        Assert.Equal("Não foi possível criar o backup. Tente novamente.", viewModel.Mensagem);
    }

    [Fact]
    public void Backup_FalhaDeDominio_NaoRegistraNoLog()
    {
        var coletor = new ColetorDeLog();
        var viewModel = CriarBackupViewModel(new FakeBackupService { ProximaCriacaoFalha = true }, coletor);

        viewModel.CriarBackupCommand.Execute(null);

        Assert.Contains("Falha simulada", viewModel.Mensagem);
        Assert.Empty(coletor.Eventos);
    }

    // ---------- Produtos ----------

    private sealed class CenarioProdutos
    {
        public required ProdutosViewModel ViewModel { get; init; }
        public required ProdutoRepositoryQueFalha Produtos { get; init; }
        public required CategoriaRepositoryQueFalha Categorias { get; init; }
        public required FotoStorageQueFalha Fotos { get; init; }
    }

    private static CenarioProdutos CriarProdutos(ColetorDeLog? coletor)
    {
        var categorias = new CategoriaRepositoryQueFalha(new FakeCategoriaRepository());
        var produtos = new ProdutoRepositoryQueFalha(new FakeProdutoRepository());
        var fotos = new FotoStorageQueFalha();
        var relogio = new FakeClock();
        new CadastrarCategoria(categorias, relogio).Executar("Bebidas");

        var viewModel = new ProdutosViewModel(
            new ListarCategoriasAtivas(categorias),
            new CadastrarCategoria(categorias, relogio),
            new PesquisarProdutos(produtos),
            new CadastrarProduto(produtos, categorias, relogio),
            new AlterarProduto(produtos, categorias, relogio),
            new DesativarProduto(produtos, relogio),
            new DefinirFotoProduto(produtos, fotos, relogio),
            new RemoverFotoProduto(produtos, fotos, relogio),
            coletor?.Logger);
        return new CenarioProdutos { ViewModel = viewModel, Produtos = produtos, Categorias = categorias, Fotos = fotos };
    }

    private static void PreencherNovoProduto(ProdutosViewModel vm)
    {
        vm.NomeProduto = "Refrigerante";
        vm.CategoriaProduto = vm.Categorias[0];
        vm.PrecoProdutoReais = "5,00";
    }

    private static void SelecionarProdutoCadastrado(CenarioProdutos c)
    {
        PreencherNovoProduto(c.ViewModel);
        c.ViewModel.SalvarCommand.Execute(null);
        c.ViewModel.ProdutoSelecionado = c.ViewModel.Produtos[0];
    }

    [Fact]
    public void Produtos_FalhaAoSalvar_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var c = CriarProdutos(coletor);
        PreencherNovoProduto(c.ViewModel);
        c.Produtos.Falhar = true;

        c.ViewModel.SalvarCommand.Execute(null);

        Assert.Equal("Não foi possível salvar o produto. Tente novamente.", c.ViewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "SalvarProduto", DadosDeNegocio);
    }

    [Fact]
    public void Produtos_FalhaAoSalvarFotoDoNovoProduto_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var c = CriarProdutos(coletor);
        PreencherNovoProduto(c.ViewModel);
        c.ViewModel.DefinirFoto("C:\\foto.jpg");
        c.Fotos.Falhar = true;

        c.ViewModel.SalvarCommand.Execute(null);

        Assert.Equal("Produto cadastrado, mas não foi possível salvar a foto. Tente novamente.", c.ViewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "SalvarFotoDoProduto", DadosDeNegocio);
    }

    [Fact]
    public void Produtos_FalhaAoDesativar_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var c = CriarProdutos(coletor);
        SelecionarProdutoCadastrado(c);
        c.Produtos.Falhar = true;

        c.ViewModel.DesativarCommand.Execute(null);

        Assert.Equal("Não foi possível desativar o produto. Tente novamente.", c.ViewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "DesativarProduto", DadosDeNegocio);
    }

    [Fact]
    public void Produtos_FalhaAoDefinirFoto_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var c = CriarProdutos(coletor);
        SelecionarProdutoCadastrado(c);
        c.Fotos.Falhar = true;

        c.ViewModel.DefinirFoto("C:\\foto.jpg");

        Assert.Equal("Não foi possível salvar a foto. Tente novamente.", c.ViewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "DefinirFoto", DadosDeNegocio);
    }

    [Fact]
    public void Produtos_FalhaAoRemoverFoto_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var c = CriarProdutos(coletor);
        SelecionarProdutoCadastrado(c);
        c.ViewModel.DefinirFoto("C:\\foto.jpg");
        c.Produtos.Falhar = true;

        c.ViewModel.RemoverFotoCommand.Execute(null);

        Assert.Equal("Não foi possível remover a foto. Tente novamente.", c.ViewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "RemoverFoto", DadosDeNegocio);
    }

    [Fact]
    public void Produtos_FalhaAoAdicionarCategoria_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var c = CriarProdutos(coletor);
        c.ViewModel.NovaCategoriaNome = "Hamburguer";
        c.Categorias.Falhar = true;

        c.ViewModel.AdicionarCategoriaCommand.Execute(null);

        Assert.Equal("Não foi possível adicionar a categoria. Tente novamente.", c.ViewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "AdicionarCategoria", DadosDeNegocio);
    }

    [Fact]
    public void Produtos_FalhaTecnicaSemLogger_MantemMensagemENaoLanca()
    {
        var c = CriarProdutos(null);
        PreencherNovoProduto(c.ViewModel);
        c.Produtos.Falhar = true;

        var excecao = Record.Exception(() => c.ViewModel.SalvarCommand.Execute(null));

        Assert.Null(excecao);
        Assert.Equal("Não foi possível salvar o produto. Tente novamente.", c.ViewModel.Mensagem);
    }

    [Fact]
    public void Produtos_FalhaDeDominio_NaoRegistraNoLog()
    {
        var coletor = new ColetorDeLog();
        var c = CriarProdutos(coletor);
        c.ViewModel.CategoriaProduto = c.ViewModel.Categorias[0];
        c.ViewModel.NomeProduto = string.Empty;
        c.ViewModel.PrecoProdutoReais = "5,00";

        c.ViewModel.SalvarCommand.Execute(null);

        Assert.NotEqual(string.Empty, c.ViewModel.Mensagem);
        Assert.Empty(coletor.Eventos);
    }

    // ---------- Configuracao ----------

    private static (ConfiguracaoViewModel ViewModel, ConfiguracaoRepositoryQueFalha Repositorio) CriarConfiguracao(ColetorDeLog? coletor)
    {
        var repositorio = new ConfiguracaoRepositoryQueFalha(new FakeConfiguracaoRepository());
        var viewModel = new ConfiguracaoViewModel(
            new ObterConfiguracao(repositorio),
            new SalvarConfiguracao(repositorio, new FakeClock()),
            coletor?.Logger);
        viewModel.NomeEstabelecimento = "Lanchonete do Ze";
        return (viewModel, repositorio);
    }

    [Fact]
    public void Configuracao_FalhaAoSalvar_RegistraErroSoComOperacaoEMantemMensagem()
    {
        var coletor = new ColetorDeLog();
        var (viewModel, repositorio) = CriarConfiguracao(coletor);
        repositorio.Falhar = true;

        viewModel.SalvarCommand.Execute(null);

        Assert.Equal("Não foi possível salvar as configurações. Tente novamente.", viewModel.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "SalvarConfiguracao", "Lanchonete");
    }

    [Fact]
    public void Configuracao_FalhaTecnicaSemLogger_MantemMensagemENaoLanca()
    {
        var (viewModel, repositorio) = CriarConfiguracao(null);
        repositorio.Falhar = true;

        var excecao = Record.Exception(() => viewModel.SalvarCommand.Execute(null));

        Assert.Null(excecao);
        Assert.Equal("Não foi possível salvar as configurações. Tente novamente.", viewModel.Mensagem);
    }
}
