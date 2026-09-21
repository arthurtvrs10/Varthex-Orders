using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Homologacao;

// CT11 — criterio de docs/docs/09-testes-aceitacao.md: uma falha entre a venda e o fechamento da
// comanda desfaz tudo (rollback). A falha e injetada de verdade: um interceptor de comandos do EF
// deixa passar o N-esimo comando de escrita do SaveChanges do encerramento (INSERT da venda ou
// UPDATE da comanda, na ordem que o EF escolher) e lanca no comando seguinte, ainda dentro da
// transacao aberta pelo SaveChanges. Depois conferimos, por contextos novos, que nada ficou.
public sealed class FalhaNoEncerramentoTests : IDisposable
{
    private readonly FalhaInjetadaNoNesimoComandoDeEscrita _falha = new();
    private readonly BancoDeHomologacao _banco;
    private readonly EfComandaRepository _comandas;

    public FalhaNoEncerramentoTests()
    {
        _banco = new BancoDeHomologacao(migrar: true, _falha);
        _comandas = new EfComandaRepository(_banco.Fabrica);
    }

    public void Dispose() => _banco.Dispose();

    private (int comandaId, long total) PrepararComandaComItens()
    {
        var refri = _banco.CriarProduto("Refrigerante", 500);
        var suco = _banco.CriarProduto("Suco", 750);
        var agora = new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc);
        var comanda = _comandas.AbrirComanda(10, agora);
        _comandas.AdicionarItem(comanda.Id, refri, 2, agora);
        var detalhe = _comandas.AdicionarItem(comanda.Id, suco, 1, agora);
        return (comanda.Id, detalhe.Comanda.TotalCentavos);
    }

    [Theory]
    [InlineData(1)] // falha ja no primeiro comando de escrita (nada foi executado antes)
    [InlineData(2)] // falha no segundo: o primeiro comando JA foi executado dentro da transacao
    [Trait("Caso", "CT11")]
    public void CT11_FalhaDuranteOEncerramento_DesfazVendaEFechamento(int falharNoComando)
    {
        var (comandaId, totalAntes) = PrepararComandaComItens();
        Assert.Equal(500 * 2 + 750, totalAntes);

        _falha.Armar(falharNoComando);
        var erro = Assert.ThrowsAny<Exception>(() => _comandas.EncerrarComanda(comandaId, new DateTime(2026, 9, 18, 16, 0, 0, DateTimeKind.Utc)));
        _falha.Desarmar();

        // a falha lancada foi a injetada (e nao um erro qualquer)
        Assert.True(TemFalhaInjetada(erro), $"a excecao nao veio da falha injetada: {erro}");
        // o interceptor so lanca no N-esimo comando de escrita: os N-1 anteriores foram MESMO executados
        Assert.Equal(falharNoComando - 1, _falha.ComandosExecutadosComSucesso);
        Assert.Equal(falharNoComando, _falha.ComandosDeEscritaVistos.Count);

        // nada sobrou: nenhuma venda, comanda ainda aberta, sem data de fechamento, itens e total intactos
        using (var contexto = _banco.Fabrica.CreateDbContext())
        {
            Assert.Empty(contexto.Vendas.ToList());
            var comanda = contexto.Comandas.Single(c => c.Id == comandaId);
            Assert.Equal(StatusComanda.Aberta, comanda.Status);
            Assert.Null(comanda.FechadaEm);
            Assert.Equal(totalAntes, comanda.TotalCentavos);
            Assert.Equal(2, contexto.ItensComanda.Count(i => i.ComandaId == comandaId));
        }
        Assert.Single(new EfComandaRepository(_banco.Fabrica).ListarAbertas());

        // sem residuo: sem a falha, o mesmo encerramento agora conclui normalmente com a venda no 1
        var venda = _comandas.EncerrarComanda(comandaId, new DateTime(2026, 9, 18, 16, 5, 0, DateTimeKind.Utc));
        Assert.Equal(1, venda.Numero);
        Assert.Equal(totalAntes, venda.TotalCentavos);
        using var contextoFinal = _banco.Fabrica.CreateDbContext();
        Assert.Single(contextoFinal.Vendas.ToList());
        Assert.Equal(StatusComanda.Fechada, contextoFinal.Comandas.Single(c => c.Id == comandaId).Status);
    }

    // Mesmo criterio, agora pelo caso de uso: a falha vira excecao (nao resultado silencioso)
    // e a comanda segue aberta para nova tentativa.
    [Fact]
    [Trait("Caso", "CT11")]
    public void CT11_CasoDeUsoEncerrarComanda_FalhaDeBanco_NaoFechaComanda()
    {
        var (comandaId, _) = PrepararComandaComItens();
        _falha.Armar(2);

        Assert.ThrowsAny<Exception>(() => new EncerrarComanda(_comandas, new RelogioFixo()).Executar(comandaId));
        _falha.Desarmar();

        using var contexto = _banco.Fabrica.CreateDbContext();
        Assert.Empty(contexto.Vendas.ToList());
        Assert.Equal(StatusComanda.Aberta, contexto.Comandas.Single(c => c.Id == comandaId).Status);
    }

    private static bool TemFalhaInjetada(Exception? excecao)
    {
        for (var e = excecao; e is not null; e = e.InnerException)
        {
            if (e is FalhaInjetadaException) return true;
        }
        return false;
    }

    private sealed class FalhaInjetadaException : Exception
    {
        public FalhaInjetadaException(string mensagem) : base(mensagem) { }
    }

    private sealed class FalhaInjetadaNoNesimoComandoDeEscrita : DbCommandInterceptor
    {
        private int _numeroDoComandoQueFalha;
        private bool _armado;

        public List<string> ComandosDeEscritaVistos { get; } = new();
        public int ComandosExecutadosComSucesso { get; private set; }

        public void Armar(int numeroDoComandoQueFalha)
        {
            _numeroDoComandoQueFalha = numeroDoComandoQueFalha;
            ComandosDeEscritaVistos.Clear();
            ComandosExecutadosComSucesso = 0;
            _armado = true;
        }

        public void Desarmar() => _armado = false;

        private static bool EhEscrita(DbCommand comando)
        {
            var texto = comando.CommandText.TrimStart();
            return texto.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
                || texto.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                || texto.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase);
        }

        private void AntesDeExecutar(DbCommand comando)
        {
            if (!_armado || !EhEscrita(comando)) return;
            ComandosDeEscritaVistos.Add(comando.CommandText);
            if (ComandosDeEscritaVistos.Count == _numeroDoComandoQueFalha)
            {
                throw new FalhaInjetadaException($"falha injetada no comando de escrita {_numeroDoComandoQueFalha}");
            }
        }

        private void DepoisDeExecutar(DbCommand comando)
        {
            if (_armado && EhEscrita(comando)) ComandosExecutadosComSucesso++;
        }

        // o provedor SQLite executa INSERT/UPDATE com "RETURNING" como leitor e os demais como nao-query
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand comando, CommandEventData dados, InterceptionResult<DbDataReader> resultado)
        {
            AntesDeExecutar(comando);
            return resultado;
        }

        public override DbDataReader ReaderExecuted(DbCommand comando, CommandExecutedEventData dados, DbDataReader resultado)
        {
            DepoisDeExecutar(comando);
            return resultado;
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand comando, CommandEventData dados, InterceptionResult<int> resultado)
        {
            AntesDeExecutar(comando);
            return resultado;
        }

        public override int NonQueryExecuted(DbCommand comando, CommandExecutedEventData dados, int resultado)
        {
            DepoisDeExecutar(comando);
            return resultado;
        }
    }
}
