using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Backup;
using VarthexComanda.Infrastructure.Configuracao;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using VarthexComanda.Infrastructure.Storage;

namespace VarthexComanda.MassaMinima;

/// <summary>Perfil de uma comanda deixada aberta na massa (para o operador saber o que esperar na tela).</summary>
public sealed record PerfilDeComandaAberta(int Numero, int Linhas, int Unidades, long TotalCentavos, string Descricao);

/// <summary>Contagens da massa gerada, lidas do proprio banco (nao de contadores internos).</summary>
public sealed record ResumoMassa(
    string PastaRaiz,
    DateTime AgoraUtc,
    int Categorias,
    int CategoriasInativas,
    int Produtos,
    int ProdutosInativos,
    int ProdutosComFoto,
    int NumerosDeComanda,
    int ComandasAbertas,
    int ComandasCanceladas,
    int Vendas,
    int ItensEmVendas,
    int DiasDeHistorico,
    int DiasComVendas,
    long TotalVendidoCentavos,
    string BackupValido,
    string BackupAntigo,
    string BackupCorrompido,
    IReadOnlyList<PerfilDeComandaAberta> PerfisDasAbertas);

/// <summary>
/// Gera a "massa minima" de homologacao (docs/docs/09-testes-aceitacao.md) numa pasta descartavel usando o
/// mesmo codigo do aplicativo: AppPaths, migracoes, repositorios EF, casos de uso e EfBackupService.
/// Reproduzivel: semente fixa e todas as datas relativas a <c>agoraUtc</c>.
/// </summary>
public static class MassaMinimaGerador
{
    public const int Semente = 20260921;
    public const int DiasDeHistoricoAlvo = 90;

    private static readonly ((byte R, byte G, byte B) Inicio, (byte R, byte G, byte B) Fim)[] CoresDasCategorias =
    [
        ((230, 126, 34), (192, 57, 43)),
        ((52, 152, 219), (41, 128, 185)),
        ((241, 196, 15), (211, 84, 0)),
        ((155, 89, 182), (142, 68, 173)),
        ((149, 165, 166), (99, 110, 114)),
    ];

    public static ResumoMassa Gerar(string pastaRaiz, DateTime agoraUtc, bool forcar = false)
    {
        // 1) seguranca primeiro: nenhum acesso ao disco antes de recusar caminhos proibidos
        var raiz = SegurancaDaSaida.ValidarOuRecusar(pastaRaiz, forcar);
        var agora = NormalizarAgora(agoraUtc);

        if (Directory.Exists(raiz))
        {
            LimparSubpastasDoAplicativo(raiz); // so chega aqui vazia, ou com --forcar numa pasta com o marcador
        }

        var paths = new AppPaths(raiz);
        paths.EnsureCreated();
        File.WriteAllText(Path.Combine(raiz, SegurancaDaSaida.NomeDoMarcador),
            "Pasta gerada por VarthexComanda.MassaMinima (dados de homologacao descartaveis)." + Environment.NewLine);

        var fabrica = new FabricaDeContexto(paths.DatabasePath);
        var relogio = new RelogioDoGerador { UtcNow = agora };
        var rnd = new Random(Semente);

        try
        {
            using (var contexto = fabrica.CreateDbContext())
            {
                contexto.Database.Migrate();
            }

            var servicos = new Servicos(fabrica, relogio);
            var hojeLocal = FusoBrasilia.ParaLocal(agora).Date;
            var inicioLocal = hojeLocal.AddDays(-DiasDeHistoricoAlvo);

            var produtos = CadastrarCatalogoEConfiguracao(servicos, relogio, paths, inicioLocal, rnd);
            SimularHistorico(servicos, relogio, produtos, hojeLocal, rnd);
            EncerrarCatalogoDescontinuado(servicos, relogio, produtos, hojeLocal);
            DeixarComandasAbertas(servicos, relogio, produtos, agora, hojeLocal);

            relogio.UtcNow = agora;
            var (valido, antigo, corrompido) = GerarBackups(paths, fabrica, relogio, agora);

            SqliteConnection.ClearAllPools();
            return LerResumo(raiz, agora, hojeLocal, fabrica, valido, antigo, corrompido);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    // ---- passo 1: catalogo, configuracao e fotos ------------------------------------------------------

    private sealed record ProdutoGerado(int Id, string Nome, long PrecoCentavos, double Peso);

    private static List<ProdutoGerado> CadastrarCatalogoEConfiguracao(
        Servicos s, RelogioDoGerador relogio, AppPaths paths, DateTime inicioLocal, Random rnd)
    {
        relogio.UtcNow = FusoBrasilia.ParaUtc(inicioLocal.AddHours(9));

        Exigir(s.SalvarConfiguracao.Executar(new ConfiguracaoEstabelecimento
        {
            NomeEstabelecimento = CatalogoDeExemplo.NomeDoEstabelecimento,
            QuantidadeMaximaComandas = CatalogoDeExemplo.QuantidadeDeComandas,
            PastaBackupExterna = null
        }));

        var gerados = new List<ProdutoGerado>();
        var indice = 0;
        // popularidade: pesos decrescentes embaralhados pela semente (alguns produtos vendem muito mais)
        var pesos = Enumerable.Range(0, 30).Select(i => 1.0 / Math.Pow(i + 1, 0.8)).OrderBy(_ => rnd.Next()).ToArray();

        for (var c = 0; c < CatalogoDeExemplo.Categorias.Count; c++)
        {
            var (nomeCategoria, itens) = CatalogoDeExemplo.Categorias[c];
            relogio.UtcNow = relogio.UtcNow.AddMinutes(1);
            var categoria = Exigir(s.CadastrarCategoria.Executar(nomeCategoria));
            var cor = CoresDasCategorias[c % CoresDasCategorias.Length];

            foreach (var item in itens)
            {
                relogio.UtcNow = relogio.UtcNow.AddMinutes(1);
                var produto = Exigir(s.CadastrarProduto.Executar(item.Nome, categoria.Id, item.PrecoCentavos));

                if (indice % 3 != 0) // 20 dos 30 produtos tem foto; os demais exercitam o "sem foto"
                {
                    var nomeArquivo = NomeDeArquivoDeFoto(rnd);
                    var bytes = PngSintetico.Gerar(Inicial(item.Nome), cor.Inicio, cor.Fim);
                    File.WriteAllBytes(Path.Combine(paths.FotosDirectory, nomeArquivo), bytes);

                    produto.FotoArquivo = nomeArquivo;
                    produto.AtualizadoEm = relogio.UtcNow;
                    s.Produtos.Salvar(produto);
                }

                gerados.Add(new ProdutoGerado(produto.Id, produto.Nome, produto.PrecoCentavos, pesos[indice]));
                indice++;
            }
        }

        return gerados;
    }

    // Nome de arquivo no formato do app (GUID "N" + .png), mas derivado da semente para ser reproduzivel.
    private static string NomeDeArquivoDeFoto(Random rnd)
    {
        var bytes = new byte[16];
        rnd.NextBytes(bytes);
        return $"{new Guid(bytes):N}.png";
    }

    private static char Inicial(string nome)
    {
        var decomposto = nome.Normalize(NormalizationForm.FormD);
        return decomposto.FirstOrDefault(char.IsLetter, '?');
    }

    // ---- passo 2: historico de 90 dias ----------------------------------------------------------------

    private static void SimularHistorico(
        Servicos s, RelogioDoGerador relogio, List<ProdutoGerado> produtos, DateTime hojeLocal, Random rnd)
    {
        for (var d = DiasDeHistoricoAlvo; d >= 1; d--)
        {
            var dia = hojeLocal.AddDays(-d);
            var metaDoDia = dia.DayOfWeek switch
            {
                DayOfWeek.Friday or DayOfWeek.Saturday => rnd.Next(9, 15),
                DayOfWeek.Sunday => rnd.Next(5, 9),
                _ => rnd.Next(6, 11)
            };

            var t = dia.AddHours(10).AddMinutes(rnd.Next(0, 40)); // hora local de Brasilia
            var limite = dia.AddHours(21).AddMinutes(30);
            for (var i = 0; i < metaDoDia && t < limite; i++)
            {
                var abandonar = i >= 3 && rnd.NextDouble() < 0.10; // as 3 primeiras sao sempre vendas
                var fim = abandonar
                    ? AtenderEAbandonar(s, relogio, produtos, t, rnd)
                    : AtenderEEncerrar(s, relogio, produtos, t, rnd);

                var pico = t.Hour is 12 or 13 or 19 or 20;
                t = fim.AddMinutes(pico ? rnd.Next(2, 12) : rnd.Next(15, 61));
            }
        }
    }

    private static DateTime AtenderEEncerrar(
        Servicos s, RelogioDoGerador relogio, List<ProdutoGerado> produtos, DateTime abertaLocal, Random rnd)
    {
        var comanda = AbrirEm(s, relogio, abertaLocal, rnd.Next(1, CatalogoDeExemplo.QuantidadeDeComandas + 1));

        var linhas = SortearLinhas(rnd);
        var escolhidos = new List<ProdutoGerado>();
        while (escolhidos.Count < linhas)
        {
            var p = Sortear(produtos, rnd);
            if (!escolhidos.Contains(p)) escolhidos.Add(p);
        }

        foreach (var p in escolhidos)
        {
            relogio.UtcNow = relogio.UtcNow.AddSeconds(rnd.Next(20, 91));
            Exigir(s.AdicionarItem.Executar(comanda.Id, p.Id, rnd.Next(1, 4)));
        }

        if (rnd.NextDouble() < 0.12) // item repetido: o app soma na mesma linha
        {
            relogio.UtcNow = relogio.UtcNow.AddSeconds(rnd.Next(20, 91));
            Exigir(s.AdicionarItem.Executar(comanda.Id, escolhidos[0].Id, 1));
        }

        relogio.UtcNow = relogio.UtcNow.AddMinutes(rnd.Next(4, 41));
        Exigir(s.EncerrarComanda.Executar(comanda.Id));
        return FusoBrasilia.ParaLocal(relogio.UtcNow);
    }

    private static DateTime AtenderEAbandonar(
        Servicos s, RelogioDoGerador relogio, List<ProdutoGerado> produtos, DateTime abertaLocal, Random rnd)
    {
        var comanda = AbrirEm(s, relogio, abertaLocal, rnd.Next(1, CatalogoDeExemplo.QuantidadeDeComandas + 1));

        if (rnd.NextDouble() < 0.6)
        {
            relogio.UtcNow = relogio.UtcNow.AddSeconds(rnd.Next(20, 91));
            Exigir(s.AdicionarItem.Executar(comanda.Id, Sortear(produtos, rnd).Id, rnd.Next(1, 3)));
        }

        relogio.UtcNow = relogio.UtcNow.AddMinutes(rnd.Next(1, 11));
        Exigir(s.CancelarComanda.Executar(comanda.Id));
        return FusoBrasilia.ParaLocal(relogio.UtcNow);
    }

    private static Comanda AbrirEm(Servicos s, RelogioDoGerador relogio, DateTime abertaLocal, int numero)
    {
        relogio.UtcNow = FusoBrasilia.ParaUtc(abertaLocal);
        return Exigir(s.AbrirComanda.Executar(numero));
    }

    private static int SortearLinhas(Random rnd)
    {
        var x = rnd.NextDouble();
        return x < 0.25 ? 1 : x < 0.60 ? 2 : x < 0.82 ? 3 : x < 0.94 ? 4 : 5;
    }

    private static ProdutoGerado Sortear(List<ProdutoGerado> produtos, Random rnd)
    {
        var alvo = rnd.NextDouble() * produtos.Sum(p => p.Peso);
        foreach (var p in produtos)
        {
            alvo -= p.Peso;
            if (alvo <= 0) return p;
        }
        return produtos[^1];
    }

    // ---- passo 3: produto/categoria descontinuados (depois de ja terem vendido) -----------------------

    private static void EncerrarCatalogoDescontinuado(
        Servicos s, RelogioDoGerador relogio, List<ProdutoGerado> produtos, DateTime hojeLocal)
    {
        relogio.UtcNow = FusoBrasilia.ParaUtc(hojeLocal.AddDays(-1).AddHours(22).AddMinutes(30));

        var descontinuado = produtos.Single(p => p.Nome == CatalogoDeExemplo.Categorias[^1].Produtos[0].Nome);
        Exigir(s.DesativarProduto.Executar(descontinuado.Id));

        var categoria = s.Categorias.ListarAtivas().Single(c => c.Nome == CatalogoDeExemplo.CategoriaInativa);
        Exigir(s.AlterarCategoria.Executar(categoria.Id, categoria.Nome, ativo: false));
    }

    // ---- passo 4: comandas abertas com perfis diferentes ----------------------------------------------

    private static void DeixarComandasAbertas(
        Servicos s, RelogioDoGerador relogio, List<ProdutoGerado> produtos, DateTime agora, DateTime hojeLocal)
    {
        // nunca antes do fim do historico (o ultimo evento dele e a desativacao, ontem 22:30 local)
        var piso = FusoBrasilia.ParaUtc(hojeLocal.AddDays(-1).AddHours(22).AddMinutes(31));

        ProdutoGerado P(string nome) => produtos.Single(p => p.Nome == nome);

        void Atender(int numero, int minutosAtras, params (string Produto, int Quantidade)[] itens)
        {
            var abertura = agora.AddMinutes(-minutosAtras);
            relogio.UtcNow = abertura < piso ? piso : abertura;
            var comanda = Exigir(s.AbrirComanda.Executar(numero));
            foreach (var (nome, quantidade) in itens)
            {
                relogio.UtcNow = relogio.UtcNow.AddSeconds(45);
                Exigir(s.AdicionarItem.Executar(comanda.Id, P(nome).Id, quantidade));
            }
        }

        // 1 item
        Atender(3, 12, ("Refrigerante Lata", 1));
        // muitos itens (9 linhas)
        Atender(7, 95,
            ("X-Burger", 2), ("X-Bacon", 1), ("Batata Frita", 1), ("Anéis de Cebola", 1), ("Refrigerante Lata", 3),
            ("Suco de Laranja", 1), ("Água Mineral", 2), ("Pudim", 2), ("Café Expresso", 2));
        // itens repetidos (o app soma na mesma linha: X-Burger x3, Batata Frita x2)
        Atender(12, 40,
            ("X-Burger", 1), ("Batata Frita", 1), ("X-Burger", 1), ("Refrigerante 600ml", 1), ("X-Burger", 1), ("Batata Frita", 1));
        // vazia (recem-aberta: nao pode ser encerrada)
        Atender(15, 5);
        // 2 itens
        Atender(18, 25, ("Misto Quente", 1), ("Chá Gelado", 1));
    }

    // ---- passo 5: backups -----------------------------------------------------------------------------

    private static (string Valido, string Antigo, string Corrompido) GerarBackups(
        AppPaths paths, FabricaDeContexto fabrica, RelogioDoGerador relogio, DateTime agora)
    {
        // valido: exatamente o que o app faz (.db + .sha256 + .fotos.zip)
        var servico = new EfBackupService(paths, new EfBackupRegistroRepository(fabrica), relogio);
        var criado = servico.CriarBackupGerenciado();
        if (!criado.Sucesso)
        {
            throw new InvalidOperationException("Falha ao criar o backup valido: " + string.Join(" ", criado.Erros));
        }
        var valido = criado.Valor!.Arquivo;

        // antigo: banco do "aplicativo antigo" (so a migracao InitialCreate), com nome padrao e .sha256 proprio
        var instanteAntigo = Truncar(agora.AddDays(-30));
        var antigo = NomePadrao(instanteAntigo);
        var pastaTemporaria = Path.Combine(Path.GetTempPath(), $"varthex-massa-antigo-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(pastaTemporaria);
            var caminhoTemporario = Path.Combine(pastaTemporaria, "antigo.db");
            CriarBancoNoEsquemaAntigo(caminhoTemporario, instanteAntigo);
            var destino = Path.Combine(paths.BackupsDirectory, antigo);
            File.Copy(caminhoTemporario, destino, overwrite: false);
            File.WriteAllText(destino + ".sha256", Sha256(File.ReadAllBytes(destino)));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(pastaTemporaria, recursive: true); } catch { /* limpeza best-effort */ }
        }

        // corrompido: nome padrao, bytes invalidos fixos por semente e .sha256 que nao confere
        var corrompido = NomePadrao(Truncar(agora.AddDays(-7)));
        var lixo = new byte[8192];
        new Random(Semente + 1).NextBytes(lixo);
        var destinoCorrompido = Path.Combine(paths.BackupsDirectory, corrompido);
        File.WriteAllBytes(destinoCorrompido, lixo);
        File.WriteAllText(destinoCorrompido + ".sha256", Sha256(Encoding.UTF8.GetBytes("conteudo-original-diferente")));

        return (valido, antigo, corrompido);
    }

    private static void CriarBancoNoEsquemaAntigo(string caminho, DateTime instante)
    {
        var fabrica = new FabricaDeContexto(caminho);
        using (var contexto = fabrica.CreateDbContext())
        {
            var primeira = contexto.Database.GetMigrations().First();
            if (!primeira.EndsWith("_InitialCreate", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A primeira migracao nao e a InitialCreate.");
            }
            contexto.GetService<IMigrator>().Migrate(primeira);
        }
        SqliteConnection.ClearAllPools();

        string Data(int dias) => instante.AddDays(-dias).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var criado = Data(20);
        using var conexao = new SqliteConnection($"Data Source={caminho};Foreign Keys=True;Pooling=False");
        conexao.Open();
        foreach (var sql in new[]
        {
            $"INSERT INTO categoria (id, nome, ativo, criado_em, atualizado_em) VALUES (1, 'Bebidas', 1, '{criado}', '{criado}'), (2, 'Lanches', 1, '{criado}', '{criado}')",
            $"INSERT INTO produto (id, categoria_id, nome, preco_centavos, ativo, criado_em, atualizado_em) VALUES (1, 1, 'Refrigerante Lata', 600, 1, '{criado}', '{criado}'), (2, 2, 'X-Burger', 1800, 1, '{criado}', '{criado}')",
            $"INSERT INTO comanda (id, numero, status, aberta_em, fechada_em, total_centavos) VALUES (1, 1, 'FECHADA', '{Data(2)}', '{Data(1)}', 2400)",
            $"INSERT INTO item_comanda (id, comanda_id, produto_id, nome_produto, preco_unitario_centavos, quantidade, subtotal_centavos, criado_em, atualizado_em) VALUES (1, 1, 2, 'X-Burger', 1800, 1, 1800, '{Data(2)}', '{Data(2)}'), (2, 1, 1, 'Refrigerante Lata', 600, 1, 600, '{Data(2)}', '{Data(2)}')",
            $"INSERT INTO venda (id, comanda_id, numero, total_centavos, finalizada_em, status) VALUES (1, 1, 1, 2400, '{Data(1)}', 'CONCLUIDA')",
            $"INSERT INTO configuracao (chave, valor, atualizado_em) VALUES ('estabelecimento.nome', '{CatalogoDeExemplo.NomeDoEstabelecimento}', '{criado}'), ('comandas.quantidade_maxima', '{CatalogoDeExemplo.QuantidadeDeComandas}', '{criado}')"
        })
        {
            using var comando = conexao.CreateCommand();
            comando.CommandText = sql;
            comando.ExecuteNonQuery();
        }
    }

    private static string NomePadrao(DateTime instanteUtc) =>
        $"varthex-comanda-{instanteUtc:yyyy-MM-dd-HHmmss}.db";

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    // ---- resumo (lido do banco gerado) ----------------------------------------------------------------

    private static ResumoMassa LerResumo(
        string raiz, DateTime agora, DateTime hojeLocal, FabricaDeContexto fabrica,
        string valido, string antigo, string corrompido)
    {
        using var db = fabrica.CreateDbContext();
        var vendas = db.Vendas.AsNoTracking().ToList();
        var datasLocais = vendas.Select(v => FusoBrasilia.ParaLocal(v.FinalizadaEm).Date).ToList();
        var primeira = datasLocais.Count == 0 ? hojeLocal : datasLocais.Min();
        var idsDeVendas = vendas.Select(v => v.ComandaId).ToHashSet();

        var abertas = db.Comandas.AsNoTracking().Where(c => c.Status == StatusComanda.Aberta).OrderBy(c => c.Numero).ToList();
        var perfis = abertas.Select(c =>
        {
            var itens = db.ItensComanda.AsNoTracking().Where(i => i.ComandaId == c.Id).ToList();
            var descricao = itens.Count == 0 ? "vazia"
                : itens.Count >= 8 ? "muitos itens"
                : itens.Count == 1 && itens[0].Quantidade == 1 ? "1 item"
                : itens.Any(i => i.Quantidade >= 2 && i.NomeProduto == "X-Burger") ? "itens repetidos (somados)"
                : $"{itens.Count} itens";
            return new PerfilDeComandaAberta(c.Numero, itens.Count, itens.Sum(i => i.Quantidade), c.TotalCentavos, descricao);
        }).ToList();

        return new ResumoMassa(
            PastaRaiz: raiz,
            AgoraUtc: agora,
            Categorias: db.Categorias.Count(),
            CategoriasInativas: db.Categorias.Count(c => !c.Ativo),
            Produtos: db.Produtos.Count(),
            ProdutosInativos: db.Produtos.Count(p => !p.Ativo),
            ProdutosComFoto: db.Produtos.Count(p => p.FotoArquivo != null),
            NumerosDeComanda: int.Parse(db.Configuracoes.Single(c => c.Chave == "comandas.quantidade_maxima").Valor, CultureInfo.InvariantCulture),
            ComandasAbertas: abertas.Count,
            ComandasCanceladas: db.Comandas.Count(c => c.Status == StatusComanda.Cancelada),
            Vendas: vendas.Count,
            ItensEmVendas: db.ItensComanda.AsEnumerable().Count(i => idsDeVendas.Contains(i.ComandaId)),
            DiasDeHistorico: (hojeLocal - primeira).Days,
            DiasComVendas: datasLocais.Distinct().Count(),
            TotalVendidoCentavos: vendas.Sum(v => v.TotalCentavos),
            BackupValido: valido,
            BackupAntigo: antigo,
            BackupCorrompido: corrompido,
            PerfisDasAbertas: perfis);
    }

    // ---- utilitarios ----------------------------------------------------------------------------------

    private static void LimparSubpastasDoAplicativo(string raiz)
    {
        // --forcar: apaga apenas os quatro subdiretorios que o proprio aplicativo cria (nada mais na pasta) e
        // SOMENTE se a pasta tem o marcador de pasta gerada por esta ferramenta (segunda barreira, alem de ValidarOuRecusar)
        foreach (var nome in new[] { "data", "logs", "backups", "fotos" })
        {
            var caminho = Path.Combine(raiz, nome);
            if (Directory.Exists(caminho))
            {
                SegurancaDaSaida.ExigirMarcadorParaLimpar(raiz);
                Directory.Delete(caminho, recursive: true);
            }
        }
    }

    private static DateTime NormalizarAgora(DateTime agoraUtc)
    {
        var utc = agoraUtc.Kind == DateTimeKind.Local ? agoraUtc.ToUniversalTime() : agoraUtc;
        return Truncar(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    private static DateTime Truncar(DateTime d) =>
        new(d.Ticks - d.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    private static T Exigir<T>(Resultado<T> resultado) where T : class =>
        resultado.Sucesso
            ? resultado.Valor!
            : throw new InvalidOperationException("Falha ao gerar a massa: " + string.Join(" ", resultado.Erros));

    private sealed class Servicos
    {
        public Servicos(FabricaDeContexto fabrica, IClock relogio)
        {
            var categorias = new EfCategoriaRepository(fabrica);
            var produtos = new EfProdutoRepository(fabrica);
            var comandas = new EfComandaRepository(fabrica);
            var configuracoes = new EfConfiguracaoRepository(fabrica);

            Categorias = categorias;
            Produtos = produtos;
            SalvarConfiguracao = new SalvarConfiguracao(configuracoes, relogio);
            CadastrarCategoria = new CadastrarCategoria(categorias, relogio);
            AlterarCategoria = new AlterarCategoria(categorias, relogio);
            CadastrarProduto = new CadastrarProduto(produtos, categorias, relogio);
            DesativarProduto = new DesativarProduto(produtos, relogio);
            AbrirComanda = new AbrirComanda(comandas, relogio, new ObterConfiguracao(configuracoes));
            AdicionarItem = new AdicionarItem(comandas, produtos, relogio);
            EncerrarComanda = new EncerrarComanda(comandas, relogio);
            CancelarComanda = new CancelarComanda(comandas, relogio);
        }

        public ICategoriaRepository Categorias { get; }
        public IProdutoRepository Produtos { get; }
        public SalvarConfiguracao SalvarConfiguracao { get; }
        public CadastrarCategoria CadastrarCategoria { get; }
        public AlterarCategoria AlterarCategoria { get; }
        public CadastrarProduto CadastrarProduto { get; }
        public DesativarProduto DesativarProduto { get; }
        public AbrirComanda AbrirComanda { get; }
        public AdicionarItem AdicionarItem { get; }
        public EncerrarComanda EncerrarComanda { get; }
        public CancelarComanda CancelarComanda { get; }
    }

    private sealed class RelogioDoGerador : IClock
    {
        public DateTime UtcNow { get; set; }
    }

    /// <summary>Mesma configuracao do app (SQLite com chaves estrangeiras), sem container de DI.</summary>
    private sealed class FabricaDeContexto : IDbContextFactory<VarthexComandaDbContext>
    {
        private readonly DbContextOptions<VarthexComandaDbContext> _opcoes;

        public FabricaDeContexto(string caminhoDoBanco)
        {
            _opcoes = new DbContextOptionsBuilder<VarthexComandaDbContext>()
                .UseSqlite($"Data Source={caminhoDoBanco};Foreign Keys=True")
                .Options;
        }

        public VarthexComandaDbContext CreateDbContext() => new(_opcoes);
    }
}
