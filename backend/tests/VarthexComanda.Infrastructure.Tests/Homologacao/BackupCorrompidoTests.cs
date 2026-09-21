using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using VarthexComanda.Application.Backup;
using VarthexComanda.Infrastructure.Backup;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Homologacao;

// CT15 — criterio de docs/docs/09-testes-aceitacao.md: um backup corrompido e rejeitado e a base
// ativa permanece inalterada. Diferente dos testes ja existentes (arquivo que nem e SQLite, ou so a
// validacao), aqui o backup e um banco REAL gerado pelo app que depois tem bytes corrompidos, e a
// restauracao e tentada de ponta a ponta com o caso de uso real (RestaurarBackup) e o servico real.
public sealed class BackupCorrompidoTests : IDisposable
{
    private readonly BancoDeHomologacao _banco = new();
    private readonly RelogioFixo _relogio = new();

    public void Dispose() => _banco.Dispose();

    private static byte[] HashDoArquivo(string caminho)
    {
        SqliteConnection.ClearAllPools();
        return SHA256.HashData(File.ReadAllBytes(caminho));
    }

    /// <summary>Base ativa com uma venda de R$ 10,00 + um backup gerenciado desse estado, ja corrompido.</summary>
    private (EfBackupService servico, string backupCorrompido) PrepararBaseAtivaEBackupCorrompido()
    {
        var produto = _banco.CriarProduto("Refrigerante", 500);
        var comandas = new EfComandaRepository(_banco.Fabrica);
        var comanda = comandas.AbrirComanda(10, _relogio.UtcNow);
        comandas.AdicionarItem(comanda.Id, produto, 2, _relogio.UtcNow);
        comandas.EncerrarComanda(comanda.Id, _relogio.UtcNow);

        var servico = new EfBackupService(_banco.Paths, new EfBackupRegistroRepository(_banco.Fabrica), _relogio);
        var backup = servico.CriarBackupGerenciado();
        Assert.True(backup.Sucesso);
        var caminho = Path.Combine(backup.Valor!.Destino, backup.Valor.Arquivo);
        Assert.True(servico.Validar(caminho).Aprovado); // controle: antes de corromper, e valido

        // corrompe o backup no meio do arquivo (mantem o cabecalho SQLite)
        SqliteConnection.ClearAllPools();
        using (var stream = new FileStream(caminho, FileMode.Open, FileAccess.Write))
        {
            stream.Seek(100, SeekOrigin.Begin);
            var lixo = new byte[200];
            new Random(42).NextBytes(lixo);
            stream.Write(lixo, 0, lixo.Length);
        }
        return (servico, caminho);
    }

    private void AssertDadosDaBaseAtivaIntactos()
    {
        using var contexto = _banco.Fabrica.CreateDbContext();
        var venda = Assert.Single(contexto.Vendas.ToList());
        Assert.Equal(1000, venda.TotalCentavos);
        Assert.Equal(1, contexto.Produtos.Count());
    }

    // Caminho da tela de Backup: RestaurarBackup valida primeiro e recusa sem tocar em nada.
    [Fact]
    [Trait("Caso", "CT15")]
    public void CT15_RestaurarBackupCorrompido_PeloCasoDeUso_RecusaESemNenhumaEscritaNaBaseAtiva()
    {
        var (servico, backupCorrompido) = PrepararBaseAtivaEBackupCorrompido();
        var hashDaBaseAtivaAntes = HashDoArquivo(_banco.CaminhoDb);
        var arquivosAntes = Directory.GetFiles(_banco.Paths.BackupsDirectory).OrderBy(f => f).ToArray();

        var relatorio = servico.Validar(backupCorrompido);
        var resultado = new RestaurarBackup(servico).Executar(backupCorrompido);

        Assert.False(relatorio.Aprovado);
        Assert.False(relatorio.IntegridadeOk);
        Assert.False(resultado.Sucesso);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(resultado.Erros)));

        // base ativa byte a byte igual; nenhuma copia preventiva ou bruta criada por engano
        Assert.Equal(hashDaBaseAtivaAntes, HashDoArquivo(_banco.CaminhoDb));
        Assert.Equal(arquivosAntes, Directory.GetFiles(_banco.Paths.BackupsDirectory).OrderBy(f => f).ToArray());
        AssertDadosDaBaseAtivaIntactos();
    }

    // Defesa em profundidade: mesmo se alguem chamar o servico direto (sem a validacao do caso de
    // uso), a restauracao falha na verificacao de integridade e os DADOS da base ativa seguem
    // intactos. (O servico grava antes uma copia preventiva; por isso aqui a comparacao e por dados.)
    [Fact]
    [Trait("Caso", "CT15")]
    public void CT15_RestaurarParaDireto_ComBackupCorrompido_FalhaEOsDadosAtivosSeguemIntactos()
    {
        var (servico, backupCorrompido) = PrepararBaseAtivaEBackupCorrompido();

        var resultado = servico.RestaurarPara(backupCorrompido);

        Assert.False(resultado.Sucesso);
        Assert.NotEmpty(resultado.Erros);
        AssertDadosDaBaseAtivaIntactos();
        using var conexao = new SqliteConnection($"Data Source={_banco.CaminhoDb};Pooling=False");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "PRAGMA integrity_check";
        Assert.Equal("ok", (string?)comando.ExecuteScalar());
    }
}
