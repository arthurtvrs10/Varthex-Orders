using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Backup;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Homologacao;

// CT16 — criterio de docs/docs/09-testes-aceitacao.md: atualizar a aplicacao completa as
// migracoes e os totais anteriores permanecem. Cenario real: um banco criado SOMENTE ate a
// migracao InitialCreate (esquema da primeira versao, sem foto_arquivo), com dados inseridos por
// SQL, e entao a sequencia de inicializacao do app (backup preventivo + Migrate) ate a ultima.
public sealed class AtualizacaoDaAplicacaoTests : IDisposable
{
    private readonly BancoDeHomologacao _banco = new(migrar: false);

    public void Dispose() => _banco.Dispose();

    private void ExecutarSql(params string[] comandos)
    {
        using var conexao = new SqliteConnection($"Data Source={_banco.CaminhoDb};Foreign Keys=True;Pooling=False");
        conexao.Open();
        foreach (var texto in comandos)
        {
            using var comando = conexao.CreateCommand();
            comando.CommandText = texto;
            comando.ExecuteNonQuery();
        }
    }

    private static T Escalar<T>(string caminho, string sql)
    {
        using var conexao = new SqliteConnection($"Data Source={caminho};Pooling=False");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        return (T)Convert.ChangeType(comando.ExecuteScalar()!, typeof(T));
    }

    private static bool TemColuna(string caminho, string tabela, string coluna) =>
        Escalar<long>(caminho, $"SELECT COUNT(*) FROM pragma_table_info('{tabela}') WHERE name = '{coluna}'") > 0;

    [Fact]
    [Trait("Caso", "CT16")]
    public void CT16_BancoNoEsquemaDaInitialCreate_AtualizaMantendoTotaisEVendas()
    {
        // 1) banco do "aplicativo antigo": migrado somente ate a InitialCreate
        string idInitialCreate;
        int totalDeMigracoes;
        using (var contexto = _banco.Fabrica.CreateDbContext())
        {
            var todas = contexto.Database.GetMigrations().ToList();
            totalDeMigracoes = todas.Count;
            Assert.True(totalDeMigracoes >= 2, "o cenario precisa de pelo menos uma migracao alem da InitialCreate");
            idInitialCreate = todas[0];
            Assert.EndsWith("_InitialCreate", idInitialCreate);
            contexto.GetService<IMigrator>().Migrate(idInitialCreate);
        }
        Assert.False(TemColuna(_banco.CaminhoDb, "produto", "foto_arquivo"), "controle: o esquema antigo nao tem foto_arquivo");

        // 2) dados de antes da atualizacao, por SQL (esquema antigo): 3 vendas (5.800), 1 comanda
        //    aberta (1.500), 1 cancelada, catalogo e configuracao
        ExecutarSql(
            "INSERT INTO categoria (id, nome, ativo, criado_em, atualizado_em) VALUES (1, 'Bebidas', 1, '2026-09-01 10:00:00', '2026-09-01 10:00:00'), (2, 'Lanches', 1, '2026-09-01 10:00:00', '2026-09-01 10:00:00')",
            "INSERT INTO produto (id, categoria_id, nome, preco_centavos, ativo, criado_em, atualizado_em) VALUES " +
            "(1, 1, 'Refrigerante', 500, 1, '2026-09-01 10:00:00', '2026-09-01 10:00:00'), " +
            "(2, 2, 'X-Burger', 1800, 1, '2026-09-01 10:00:00', '2026-09-01 10:00:00'), " +
            "(3, 1, 'Suco', 600, 0, '2026-09-01 10:00:00', '2026-09-01 10:00:00')",
            "INSERT INTO comanda (id, numero, status, aberta_em, fechada_em, total_centavos) VALUES " +
            "(1, 1, 'FECHADA', '2026-09-10 14:40:00', '2026-09-10 15:00:00', 1800), " +
            "(2, 2, 'FECHADA', '2026-09-11 18:00:00', '2026-09-11 18:30:00', 2800), " +
            "(3, 3, 'FECHADA', '2026-09-12 11:30:00', '2026-09-12 12:00:00', 1200), " +
            "(4, 4, 'ABERTA',  '2026-09-13 10:00:00', NULL, 1500), " +
            "(5, 5, 'CANCELADA', '2026-09-13 10:05:00', '2026-09-13 10:06:00', 0)",
            "INSERT INTO item_comanda (id, comanda_id, produto_id, nome_produto, preco_unitario_centavos, quantidade, subtotal_centavos, criado_em, atualizado_em) VALUES " +
            "(1, 1, 2, 'X-Burger', 1800, 1, 1800, '2026-09-10 14:41:00', '2026-09-10 14:41:00'), " +
            "(2, 2, 1, 'Refrigerante', 500, 2, 1000, '2026-09-11 18:01:00', '2026-09-11 18:01:00'), " +
            "(3, 2, 2, 'X-Burger', 1800, 1, 1800, '2026-09-11 18:02:00', '2026-09-11 18:02:00'), " +
            "(4, 3, 3, 'Suco', 600, 2, 1200, '2026-09-12 11:31:00', '2026-09-12 11:31:00'), " +
            "(5, 4, 1, 'Refrigerante', 500, 3, 1500, '2026-09-13 10:01:00', '2026-09-13 10:01:00')",
            "INSERT INTO venda (id, comanda_id, numero, total_centavos, finalizada_em, status) VALUES " +
            "(1, 1, 1, 1800, '2026-09-10 15:00:00', 'CONCLUIDA'), " +
            "(2, 2, 2, 2800, '2026-09-11 18:30:00', 'CONCLUIDA'), " +
            "(3, 3, 3, 1200, '2026-09-12 12:00:00', 'CONCLUIDA')",
            "INSERT INTO configuracao (chave, valor, atualizado_em) VALUES ('comandas.quantidade_maxima', '30', '2026-09-01 10:00:00')");

        // 3) "abre a versao nova": a mesma sequencia do App.OnStartup — migracoes pendentes com
        //    dados existentes exigem backup preventivo, e so entao Migrate()
        using (var contexto = _banco.Fabrica.CreateDbContext())
        {
            Assert.True(MigracaoDoBanco.ExigeBackupPreventivo(contexto.Database, out var pendentes));
            Assert.Equal(totalDeMigracoes - 1, pendentes);

            var servico = new EfBackupService(_banco.Paths, new EfBackupRegistroRepository(_banco.Fabrica), new RelogioFixo());
            var preventivo = servico.CriarBackupGerenciado();
            Assert.True(preventivo.Sucesso);
            var caminhoPreventivo = Path.Combine(preventivo.Valor!.Destino, preventivo.Valor.Arquivo);
            // o backup preventivo guarda o estado ANTIGO (voltar atras e possivel)
            Assert.False(TemColuna(caminhoPreventivo, "produto", "foto_arquivo"));
            Assert.Equal(5800L, Escalar<long>(caminhoPreventivo, "SELECT SUM(total_centavos) FROM venda"));

            contexto.Database.Migrate();

            Assert.Empty(contexto.Database.GetPendingMigrations());
            Assert.Equal(totalDeMigracoes, contexto.Database.GetAppliedMigrations().Count());
        }
        SqliteConnection.ClearAllPools();

        // 4) esquema novo presente, dados antigos intactos
        Assert.True(TemColuna(_banco.CaminhoDb, "produto", "foto_arquivo"));
        Assert.Equal(0L, Escalar<long>(_banco.CaminhoDb, "SELECT COUNT(*) FROM produto WHERE foto_arquivo IS NOT NULL"));
        Assert.Equal(3L, Escalar<long>(_banco.CaminhoDb, "SELECT COUNT(*) FROM produto"));
        Assert.Equal("ok", Escalar<string>(_banco.CaminhoDb, "PRAGMA integrity_check"));

        using var depois = _banco.Fabrica.CreateDbContext();

        var vendas = depois.Vendas.AsNoTracking().OrderBy(v => v.Numero).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, vendas.Select(v => v.Numero).ToArray());
        Assert.Equal(new long[] { 1800, 2800, 1200 }, vendas.Select(v => v.TotalCentavos).ToArray());
        Assert.Equal(5800, vendas.Sum(v => v.TotalCentavos));
        Assert.Equal(new DateTime(2026, 9, 10, 15, 0, 0).Ticks, vendas[0].FinalizadaEm.Ticks);
        Assert.All(vendas, v => Assert.Equal(StatusVenda.Concluida, v.Status));

        // o historico (repositorio real) enxerga as vendas antigas e seus itens
        var historico = new EfVendaRepository(_banco.Fabrica)
            .ListarPorData(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(3, historico.Count);
        var itensDaVenda2 = new EfVendaRepository(_banco.Fabrica).BuscarItensDaVenda(2)!;
        Assert.Equal(new[] { "Refrigerante", "X-Burger" }, itensDaVenda2.OrderBy(i => i.Id).Select(i => i.NomeProduto).ToArray());
        Assert.Equal(2800, itensDaVenda2.Sum(i => i.SubtotalCentavos));

        // total de cada comanda fechada continua igual a soma dos seus itens e a venda correspondente
        foreach (var venda in vendas)
        {
            var soma = depois.ItensComanda.Where(i => i.ComandaId == venda.ComandaId).Sum(i => i.SubtotalCentavos);
            Assert.Equal(venda.TotalCentavos, soma);
            Assert.Equal(venda.TotalCentavos, depois.Comandas.Single(c => c.Id == venda.ComandaId).TotalCentavos);
        }

        // a comanda que estava aberta continua aberta, com os mesmos itens e total
        var abertas = new EfComandaRepository(_banco.Fabrica).ListarAbertas();
        var aberta = Assert.Single(abertas);
        Assert.Equal(4, aberta.Numero);
        var detalhe = new EfComandaRepository(_banco.Fabrica).BuscarComItens(aberta.Id)!;
        Assert.Equal(1500, detalhe.Comanda.TotalCentavos);
        var item = Assert.Single(detalhe.Itens);
        Assert.Equal(("Refrigerante", 3, 500L, 1500L), (item.NomeProduto, item.Quantidade, item.PrecoUnitarioCentavos, item.SubtotalCentavos));
        Assert.Equal(StatusComanda.Cancelada, depois.Comandas.Single(c => c.Id == 5).Status);

        // catalogo e configuracao intactos; produto antigo lido pelo modelo novo tem foto nula
        var produtos = depois.Produtos.AsNoTracking().OrderBy(p => p.Id).ToList();
        Assert.Equal(new[] { "Refrigerante", "X-Burger", "Suco" }, produtos.Select(p => p.Nome).ToArray());
        Assert.Equal(new long[] { 500, 1800, 600 }, produtos.Select(p => p.PrecoCentavos).ToArray());
        Assert.All(produtos, p => Assert.Null(p.FotoArquivo));
        Assert.Equal("30", depois.Configuracoes.Single(c => c.Chave == "comandas.quantidade_maxima").Valor);

        // o recurso novo (foto) funciona sobre a base atualizada, e uma segunda abertura nao muda nada
        var repositorioProdutos = new EfProdutoRepository(_banco.Fabrica);
        var refri = repositorioProdutos.BuscarPorId(1)!;
        refri.FotoArquivo = "refrigerante.png";
        repositorioProdutos.Salvar(refri);
        Assert.Equal("refrigerante.png", new EfProdutoRepository(_banco.Fabrica).BuscarPorId(1)!.FotoArquivo);
        using (var contexto = _banco.Fabrica.CreateDbContext())
        {
            contexto.Database.Migrate(); // idempotente
            Assert.Equal(5800, contexto.Vendas.Sum(v => v.TotalCentavos));
        }
    }
}
