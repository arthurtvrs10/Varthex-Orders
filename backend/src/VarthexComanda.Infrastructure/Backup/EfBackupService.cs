using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Logging;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Storage;

namespace VarthexComanda.Infrastructure.Backup;

public class EfBackupService : IBackupService
{
    private readonly AppPaths _paths;
    private readonly IBackupRegistroRepository _registros;
    private readonly IClock _relogio;
    private readonly int _retencaoMaxima;
    private readonly ILogger? _logger;
    private readonly PastasMascaradas? _pastasMascaradas;

    private const string SufixoZipDeFotos = ".fotos.zip";
    private const long TamanhoMaximoFotoBytes = 10L * 1024 * 1024;
    private const int MaximoEntradasNoZipDeFotos = 5000;

    private const int ZipsDeFotosMantidos = 3;
    private const long LimiteTotalRestauracaoPadraoBytes = 2L * 1024 * 1024 * 1024;

    private readonly long _limiteTotalRestauracaoBytes;

    public EfBackupService(AppPaths paths, IBackupRegistroRepository registros, IClock relogio, int retencaoMaxima = 30, ILogger? logger = null, PastasMascaradas? pastasMascaradas = null, long limiteTotalRestauracaoFotosBytes = LimiteTotalRestauracaoPadraoBytes)
    {
        _paths = paths;
        _registros = registros;
        _relogio = relogio;
        _retencaoMaxima = retencaoMaxima;
        _logger = logger;
        _pastasMascaradas = pastasMascaradas;
        _limiteTotalRestauracaoBytes = limiteTotalRestauracaoFotosBytes;
    }

    public Resultado<BackupRegistro> CriarBackupGerenciado() =>
        CriarBackupInterno(_paths.BackupsDirectory, aplicarRetencao: true);

    public Resultado<BackupRegistro> CriarBackupExterno(string pastaExterna)
    {
        // RF26: a pasta externa e conhecida so em tempo de execucao; registra-se ANTES de usa-la para
        // que mensagens de erro contendo o caminho sejam mascaradas no log.
        _pastasMascaradas?.Adicionar(PastasMascaradas.TokenPastaBackupExterna, pastaExterna);
        return CriarBackupInterno(pastaExterna, aplicarRetencao: false);
    }

    private Resultado<BackupRegistro> CriarBackupInterno(string pastaDestino, bool aplicarRetencao)
    {
        var agora = _relogio.UtcNow;
        LimparZipsTemporariosAntigos(pastaDestino);
        var nomeArquivo = $"varthex-comanda-{agora:yyyy-MM-dd-HHmmss}.db";
        var destinoFinal = Path.Combine(pastaDestino, nomeArquivo);

        // Duas chamadas dentro do mesmo segundo do relógio (ex.: a cópia preventiva
        // criada por RestaurarPara logo após um backup manual) gerariam o mesmo
        // nome de arquivo; desambiguamos com um sufixo para nunca sobrescrever um
        // backup existente.
        var sufixo = 1;
        while (File.Exists(destinoFinal))
        {
            nomeArquivo = $"varthex-comanda-{agora:yyyy-MM-dd-HHmmss}-{sufixo}.db";
            destinoFinal = Path.Combine(pastaDestino, nomeArquivo);
            sufixo++;
        }

        var destinoTemporario = destinoFinal + ".tmp";

        try
        {
            Directory.CreateDirectory(pastaDestino);

            using (var origem = new SqliteConnection($"Data Source={_paths.DatabasePath}"))
            using (var destino = new SqliteConnection($"Data Source={destinoTemporario};Pooling=False"))
            {
                origem.Open();
                destino.Open();
                origem.BackupDatabase(destino);
            }

            if (!VerificarIntegridade(destinoTemporario))
            {
                File.Delete(destinoTemporario);
                var registroFalha = new BackupRegistro
                {
                    Id = 0,
                    Arquivo = nomeArquivo,
                    Destino = pastaDestino,
                    CriadoEm = agora,
                    Status = StatusBackup.Falha,
                    Checksum = null,
                    Mensagem = "Falha na verificação de integridade do backup."
                };
                _registros.Registrar(registroFalha);
                return Resultado<BackupRegistro>.Falha(registroFalha.Mensagem!);
            }

            var checksum = CalcularChecksumSha256(destinoTemporario);
            File.Move(destinoTemporario, destinoFinal);
            File.WriteAllText(destinoFinal + ".sha256", checksum);

            var registro = new BackupRegistro
            {
                Id = 0,
                Arquivo = nomeArquivo,
                Destino = pastaDestino,
                CriadoEm = agora,
                Status = StatusBackup.Sucesso,
                Checksum = checksum,
                Mensagem = null
            };
            try
            {
                _registros.Registrar(registro);
            }
            catch (Exception exRegistro)
            {
                // o arquivo ja esta no lugar e e valido: falhar aqui nao pode virar Falha nem deixar o
                // arquivo fora da retencao; segue com o registro em memoria
                _logger?.Warning(exRegistro, "Falha em {Operacao}", "RegistrarBackup");
            }

            // as fotos entram DEPOIS de o .db estar no lugar e registrado: qualquer falha ao zipar e so
            // registrada em log e nunca afeta o resultado do backup do banco
            ZiparFotosDoBackup(destinoFinal + SufixoZipDeFotos, pastaDestino);

            // a limpeza e "melhor esforco": falhar aqui nunca transforma um backup gravado em Falha
            if (aplicarRetencao)
            {
                try
                {
                    AplicarRetencao(pastaDestino);
                }
                catch (Exception exRetencao)
                {
                    _logger?.Warning(exRetencao, "Falha em {Operacao}", "AplicarRetencao");
                }
            }

            // zips: so os 3 mais recentes, na pasta gerenciada e na externa (la nada alem de zips e tocado)
            try
            {
                PodarZipsDeFotos(pastaDestino);
            }
            catch (Exception exPoda)
            {
                _logger?.Warning(exPoda, "Falha em {Operacao}", "PodarZipsDeFotos");
            }

            return Resultado<BackupRegistro>.Ok(registro);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "CriarBackup");
            if (File.Exists(destinoTemporario))
            {
                File.Delete(destinoTemporario);
            }

            var registroFalha = new BackupRegistro
            {
                Id = 0,
                Arquivo = nomeArquivo,
                Destino = pastaDestino,
                CriadoEm = agora,
                Status = StatusBackup.Falha,
                Checksum = null,
                Mensagem = ex.Message
            };
            try
            {
                _registros.Registrar(registroFalha);
            }
            catch (Exception exRegistro)
            {
                // nao mascarar a falha original
                _logger?.Warning(exRegistro, "Falha em {Operacao}", "RegistrarFalhaDeBackup");
            }
            return Resultado<BackupRegistro>.Falha(ex.Message);
        }
    }

    private static bool VerificarIntegridade(string caminhoArquivo)
    {
        try
        {
            using var conexao = new SqliteConnection($"Data Source={caminhoArquivo};Pooling=False");
            conexao.Open();
            using var comando = conexao.CreateCommand();
            comando.CommandText = "PRAGMA integrity_check";
            return (string?)comando.ExecuteScalar() == "ok";
        }
        catch (SqliteException)
        {
            // Corrupção severa pode fazer o próprio PRAGMA falhar em vez de retornar
            // uma lista de problemas — nesse caso o arquivo também é considerado corrompido.
            return false;
        }
    }

    private static bool TemCabecalhoSqliteValido(string caminhoArquivo)
    {
        // Usamos uma conexão SQLite (em vez de ler os bytes do arquivo diretamente)
        // para essa checagem porque conexões SQLite concorrentes para o mesmo
        // arquivo são compatíveis entre si por design, enquanto um FileStream bruto
        // pode colidir com um handle nativo ainda aberto (pooling) de uma conexão
        // recém-usada nesse mesmo arquivo. "PRAGMA schema_version" só toca o
        // cabeçalho de 100 bytes do arquivo (sem precisar percorrer páginas de
        // dados), então distingue "não é SQLite" de "SQLite corrompido" mesmo
        // quando a corrupção afeta apenas o conteúdo após o cabeçalho.
        try
        {
            using var conexao = new SqliteConnection($"Data Source={caminhoArquivo};Pooling=False");
            conexao.Open();
            using var comando = conexao.CreateCommand();
            comando.CommandText = "PRAGMA schema_version";
            comando.ExecuteScalar();
            return true;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private void AplicarRetencao(string pasta)
    {
        var arquivos = Directory.GetFiles(pasta, "varthex-comanda-*.db")
            .OrderByDescending(f => f)
            .Skip(_retencaoMaxima)
            .ToList();

        foreach (var arquivo in arquivos)
        {
            try
            {
                File.Delete(arquivo);
                var companheiro = arquivo + ".sha256";
                if (File.Exists(companheiro))
                {
                    File.Delete(companheiro);
                }
            }
            catch (Exception)
            {
                // um arquivo travado nao impede a limpeza dos demais; so o nome da operacao vai ao log
                _logger?.Warning("Falha em {Operacao}", "AplicarRetencao");
            }
            // o .fotos.zip NAO e apagado aqui: quem o poda e a regra "so os 3 mais recentes" (PodarZipsDeFotos);
            // com a biblioteca inalterada o zip mais recente pertence a um backup antigo e ainda e o unico.
        }
    }

    // ---- fotos no backup -------------------------------------------------------------------------------

    private const string NomeDoManifesto = "_manifest.txt";
    private const string PadraoZipDeFotos = "varthex-comanda-*" + SufixoZipDeFotos;

    private static readonly TimeSpan IdadeMaximaDoZipTemporario = TimeSpan.FromHours(1);

    /// <summary>Chave de ordenacao cronologica do nome "varthex-comanda-yyyy-MM-dd-HHmmss[-n].db[.fotos.zip]".</summary>
    private static (string Instante, int Sequencia) ChaveDoNome(string caminho)
    {
        var nome = Path.GetFileName(caminho);
        const int tamanhoPrefixoData = 16 + 17; // "varthex-comanda-" + "yyyy-MM-dd-HHmmss"
        if (nome.Length < tamanhoPrefixoData)
        {
            return (nome, 0);
        }

        var instante = nome[..tamanhoPrefixoData];
        var resto = nome[tamanhoPrefixoData..];
        var sequencia = 0;
        if (resto.StartsWith('-'))
        {
            var fim = resto.IndexOf('.');
            _ = int.TryParse(fim > 1 ? resto[1..fim] : string.Empty, out sequencia);
        }

        return (instante, sequencia);
    }

    /// <summary>Zips de fotos completos da pasta (nunca .tmp), do mais recente para o mais antigo.</summary>
    private static List<string> ListarZipsDeFotos(string pasta)
    {
        if (!Directory.Exists(pasta))
        {
            return new List<string>();
        }

        // o EndsWith afasta o casamento "curinga" de extensao do Windows (ex.: ....zip.tmp)
        return Directory.GetFiles(pasta, PadraoZipDeFotos)
            .Where(f => f.EndsWith(SufixoZipDeFotos, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => ChaveDoNome(f).Instante, StringComparer.Ordinal)
            .ThenByDescending(f => ChaveDoNome(f).Sequencia)
            .ToList();
    }

    /// <summary>Remove .fotos.zip.tmp abandonados (mais de 1 hora) da pasta. Nunca lanca.</summary>
    private void LimparZipsTemporariosAntigos(string pasta)
    {
        try
        {
            if (!Directory.Exists(pasta))
            {
                return;
            }

            var limite = DateTime.UtcNow - IdadeMaximaDoZipTemporario;
            foreach (var tmp in Directory.GetFiles(pasta, PadraoZipDeFotos + ".tmp")
                         .Where(f => f.EndsWith(SufixoZipDeFotos + ".tmp", StringComparison.OrdinalIgnoreCase)))
            {
                if (File.GetLastWriteTimeUtc(tmp) < limite)
                {
                    File.Delete(tmp);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Falha em {Operacao}", "LimparZipTemporarioDeFotos");
        }
    }

    /// <summary>Mantem so os 3 zips de fotos mais recentes da pasta (gerenciada ou externa); nao toca em mais nada.</summary>
    private void PodarZipsDeFotos(string pasta)
    {
        LimparZipsTemporariosAntigos(pasta);
        foreach (var antigo in ListarZipsDeFotos(pasta).Skip(ZipsDeFotosMantidos))
        {
            try
            {
                File.Delete(antigo);
            }
            catch (Exception)
            {
                // um zip travado nao impede a poda dos demais; so o nome da operacao vai ao log
                _logger?.Warning("Falha em {Operacao}", "PodarZipsDeFotos");
            }
        }
    }

    private static bool EhArquivoDeFoto(string nome) => ArquivoFotoStorage.ExtensaoPermitida(nome);

    private readonly record struct FotoParaZip(string Caminho, string Nome, long Tamanho, long TicksDeEscrita);

    /// <summary>SHA-256 da lista ordenada (nome, tamanho, ultima escrita UTC) das fotos: identifica o conjunto.</summary>
    private static string ImpressaoDigitalDasFotos(IEnumerable<FotoParaZip> fotos)
    {
        var texto = string.Join(
            "\n",
            fotos.OrderBy(f => f.Nome, StringComparer.Ordinal).Select(f => $"{f.Nome}|{f.Tamanho}|{f.TicksDeEscrita}"));
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();
    }

    private static string? LerManifesto(string caminhoZip)
    {
        try
        {
            using var zip = ZipFile.OpenRead(caminhoZip);
            var entrada = zip.GetEntry(NomeDoManifesto);
            if (entrada is null || entrada.Length > 1024)
            {
                return null;
            }

            using var leitor = new StreamReader(entrada.Open());
            return leitor.ReadToEnd().Trim();
        }
        catch (Exception)
        {
            // zip ilegivel = sem manifesto: o conjunto sera considerado alterado
            return null;
        }
    }

    /// <summary>
    /// Cria o zip de fotos ao lado do backup (.db.fotos.zip) a partir de <c>fotos\</c>, mas SO quando o conjunto de
    /// fotos mudou desde o zip mais recente da pasta de destino (comparando a impressao digital guardada em
    /// <c>_manifest.txt</c>): sem isso, cada backup copiaria a biblioteca inteira. Só imagens da raiz da pasta
    /// entram, com o nome puro. Cada foto e tolerante a falha individual (ex.: arquivo travado): entra o que
    /// puder; se nenhuma entrar, nao ha zip. O zip nasce como .tmp e so recebe o nome final quando completo.
    /// Nunca lanca: falhas viram Warning e o backup do banco segue valido.
    /// </summary>
    private void ZiparFotosDoBackup(string caminhoZip, string pastaDestino)
    {
        var temporario = caminhoZip + ".tmp";
        try
        {
            if (!Directory.Exists(_paths.FotosDirectory))
            {
                return;
            }

            var fotos = Directory.GetFiles(_paths.FotosDirectory)
                .Where(f => EhArquivoDeFoto(f))
                .Select(f =>
                {
                    var info = new FileInfo(f);
                    return new FotoParaZip(f, info.Name, info.Length, info.LastWriteTimeUtc.Ticks);
                })
                .ToList();
            if (fotos.Count == 0)
            {
                return;
            }

            var impressaoDigital = ImpressaoDigitalDasFotos(fotos);
            var maisRecente = ListarZipsDeFotos(pastaDestino).FirstOrDefault();
            if (maisRecente is not null && LerManifesto(maisRecente) == impressaoDigital)
            {
                return; // conjunto inalterado: o zip mais recente ja o contem
            }

            var adicionadas = new List<FotoParaZip>();
            using (var fluxo = new FileStream(temporario, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var zip = new ZipArchive(fluxo, ZipArchiveMode.Create))
            {
                foreach (var foto in fotos)
                {
                    FileStream origem;
                    try
                    {
                        origem = new FileStream(foto.Caminho, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    catch (Exception)
                    {
                        // sem a excecao: a mensagem carregaria o nome do arquivo; so o nome da operacao vai ao log
                        _logger?.Warning("Falha em {Operacao}", "ZiparFotosDoBackup");
                        continue;
                    }

                    using (origem)
                    {
                        var entrada = zip.CreateEntry(foto.Nome, CompressionLevel.Fastest);
                        using var destino = entrada.Open();
                        origem.CopyTo(destino);
                    }

                    adicionadas.Add(foto);
                }

                if (adicionadas.Count > 0)
                {
                    // conjunto incompleto (foto pulada) gera outra impressao digital: o proximo backup tenta de novo
                    var manifesto = zip.CreateEntry(NomeDoManifesto);
                    using var escritor = new StreamWriter(manifesto.Open());
                    escritor.Write(ImpressaoDigitalDasFotos(adicionadas));
                }
            }

            if (adicionadas.Count == 0)
            {
                File.Delete(temporario);
                return;
            }

            File.Move(temporario, caminhoZip, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Falha em {Operacao}", "ZiparFotosDoBackup");
            try
            {
                if (File.Exists(temporario))
                {
                    File.Delete(temporario);
                }
            }
            catch (Exception exLimpeza)
            {
                _logger?.Warning(exLimpeza, "Falha em {Operacao}", "LimparZipTemporarioDeFotos");
            }
        }
    }

    private static bool ChaveEhMenorOuIgual((string Instante, int Sequencia) a, (string Instante, int Sequencia) b)
    {
        var c = string.CompareOrdinal(a.Instante, b.Instante);
        return c < 0 || (c == 0 && a.Sequencia <= b.Sequencia);
    }

    /// <summary>
    /// Zip de fotos da restauracao: o do proprio backup; se aquele backup nao gerou um (biblioteca inalterada), o
    /// mais recente feito ATE o instante do backup (representa as fotos daquela epoca); se o backup e anterior a
    /// todos os zips, o mais antigo. Um zip "a mais" e inofensivo: a extracao nunca apaga nada, so
    /// cria/sobrescreve por nome. Nome fora do padrao (sem instante): o mais recente da pasta.
    /// </summary>
    private static string? EscolherZipDeFotosParaRestauracao(string caminhoBackup)
    {
        var proprio = caminhoBackup + SufixoZipDeFotos;
        if (File.Exists(proprio))
        {
            return proprio;
        }

        var pasta = Path.GetDirectoryName(Path.GetFullPath(caminhoBackup));
        if (pasta is null)
        {
            return null;
        }

        var zips = ListarZipsDeFotos(pasta); // do mais recente para o mais antigo
        if (zips.Count == 0)
        {
            return null;
        }

        if (!Path.GetFileName(caminhoBackup).StartsWith("varthex-comanda-", StringComparison.OrdinalIgnoreCase))
        {
            return zips[0];
        }

        var chaveDoBackup = ChaveDoNome(caminhoBackup);
        return zips.FirstOrDefault(z => ChaveEhMenorOuIgual(ChaveDoNome(z), chaveDoBackup)) ?? zips[^1];
    }

    /// <summary>
    /// Escolhe o zip de fotos e o copia para um .tmp ao lado de <c>fotos\</c> (a raiz de dados) ANTES da copia
    /// preventiva: ela pode criar um zip novo e a poda pode apagar o zip original. Devolve null se nao ha zip ou
    /// se a copia falhar (Warning; a restauracao do banco segue sem fotos).
    /// </summary>
    private string? PrepararZipDeFotosParaRestauracao(string caminhoBackup)
    {
        try
        {
            var escolhido = EscolherZipDeFotosParaRestauracao(caminhoBackup);
            if (escolhido is null)
            {
                return null;
            }

            Directory.CreateDirectory(_paths.Root);
            var copia = Path.Combine(_paths.Root, "restaurando-fotos.tmp");
            File.Copy(escolhido, copia, overwrite: true);
            return copia;
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Falha em {Operacao}", "RestaurarFotosDoBackup");
            return null;
        }
    }

    /// <summary>
    /// Extrai as fotos do zip para <c>fotos\</c> sem apagar nada: mesmo nome sobrescreve, o resto permanece.
    /// Só o nome puro da entrada é usado (nada de caminhos), só extensões de imagem, entradas com tamanho
    /// declarado acima de 10 MB são ignoradas, no máximo 5000 entradas são lidas e o total extraído para no
    /// limite configurado (2 GB). Nunca lança.
    /// </summary>
    private void RestaurarFotosDoBackup(string? caminhoZip)
    {
        try
        {
            if (caminhoZip is null || !File.Exists(caminhoZip))
            {
                return;
            }

            Directory.CreateDirectory(_paths.FotosDirectory);
            // o ZipArchive ja le o diretorio central inteiro ao abrir; o que se controla e quantas entradas sao tratadas
            using var zip = ZipFile.OpenRead(caminhoZip);
            var entradas = zip.Entries;
            var total = 0L;
            for (var i = 0; i < Math.Min(entradas.Count, MaximoEntradasNoZipDeFotos); i++)
            {
                try
                {
                    if (!ExtrairFoto(entradas[i], ref total))
                    {
                        _logger?.Warning("Falha em {Operacao}", "RestaurarFotosDoBackup");
                        break; // limite total atingido: nao extrai mais nada
                    }
                }
                catch (Exception ex)
                {
                    // uma foto ruim nao impede as demais
                    _logger?.Warning(ex, "Falha em {Operacao}", "RestaurarFotoDoBackup");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Falha em {Operacao}", "RestaurarFotosDoBackup");
        }
    }

    /// <returns>false quando o limite total de bytes extraidos foi atingido (a foto atual e descartada).</returns>
    private bool ExtrairFoto(ZipArchiveEntry entrada, ref long totalExtraido)
    {
        // Path.GetFileName descarta qualquer diretorio ("..\..\x.png" vira "x.png"): sem zip-slip
        var nome = Path.GetFileName(entrada.FullName);
        if (string.IsNullOrEmpty(nome)
            || nome.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !EhArquivoDeFoto(nome)
            || entrada.Length > TamanhoMaximoFotoBytes)
        {
            return true;
        }

        var destino = Path.Combine(_paths.FotosDirectory, nome);
        var temporario = destino + ".restaurando";
        try
        {
            long desta = 0;
            using (var origem = entrada.Open())
            using (var saida = new FileStream(temporario, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // o tamanho declarado pode mentir: limita o que realmente e lido
                var buffer = new byte[81920];
                int lidos;
                while ((lidos = origem.Read(buffer, 0, buffer.Length)) > 0)
                {
                    desta += lidos;
                    if (desta > TamanhoMaximoFotoBytes)
                    {
                        throw new InvalidDataException("Foto acima do tamanho máximo.");
                    }

                    if (totalExtraido + desta > _limiteTotalRestauracaoBytes)
                    {
                        return false;
                    }

                    saida.Write(buffer, 0, lidos);
                }
            }

            File.Move(temporario, destino, overwrite: true);
            totalExtraido += desta;
            return true;
        }
        finally
        {
            if (File.Exists(temporario))
            {
                File.Delete(temporario);
            }
        }
    }

    private static string CalcularChecksumSha256(string caminhoArquivo)
    {
        using var stream = File.OpenRead(caminhoArquivo);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public RelatorioValidacao Validar(string caminhoArquivo)
    {
        if (!File.Exists(caminhoArquivo)
            || !caminhoArquivo.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            || !TemCabecalhoSqliteValido(caminhoArquivo))
        {
            return new RelatorioValidacao
            {
                FormatoValido = false,
                VersaoCompativel = false,
                IntegridadeOk = false,
                ChecksumConfere = null,
                Motivo = "Arquivo não encontrado ou não é um banco .db."
            };
        }

        // A partir daqui o cabeçalho SQLite já foi validado, então o arquivo tem o
        // formato correto mesmo que esteja corrompido internamente — corrupção de
        // dados é responsabilidade da checagem de integridade abaixo, não do formato.
        var versaoCompativel = true;
        try
        {
            var opcoes = new DbContextOptionsBuilder<VarthexComandaDbContext>()
                .UseSqlite($"Data Source={caminhoArquivo};Pooling=False")
                .Options;
            using var contexto = new VarthexComandaDbContext(opcoes);
            var aplicadas = contexto.Database.GetAppliedMigrations().ToList();
            var conhecidas = contexto.Database.GetMigrations().ToList();
            var ultimaAplicada = aplicadas.LastOrDefault();
            versaoCompativel = ultimaAplicada is null || conhecidas.Contains(ultimaAplicada);
        }
        catch (SqliteException)
        {
            // Não foi possível ler o histórico de migrações (provável corrupção de
            // dados). Não tratamos isso como incompatibilidade de versão; a
            // checagem de integridade abaixo é quem vai reportar o problema real.
            versaoCompativel = true;
        }

        if (!versaoCompativel)
        {
            return new RelatorioValidacao
            {
                FormatoValido = true,
                VersaoCompativel = false,
                IntegridadeOk = false,
                ChecksumConfere = null,
                Motivo = "Backup de uma versão incompatível do Varthex Comanda."
            };
        }

        if (!VerificarIntegridade(caminhoArquivo))
        {
            return new RelatorioValidacao
            {
                FormatoValido = true,
                VersaoCompativel = true,
                IntegridadeOk = false,
                ChecksumConfere = null,
                Motivo = "Arquivo de backup está corrompido (falhou na verificação de integridade)."
            };
        }

        bool? checksumConfere = null;
        var companheiro = caminhoArquivo + ".sha256";
        if (File.Exists(companheiro))
        {
            var esperado = File.ReadAllText(companheiro).Trim();
            var calculado = CalcularChecksumSha256(caminhoArquivo);
            checksumConfere = string.Equals(esperado, calculado, StringComparison.OrdinalIgnoreCase);
        }

        return new RelatorioValidacao
        {
            FormatoValido = true,
            VersaoCompativel = true,
            IntegridadeOk = true,
            ChecksumConfere = checksumConfere,
            Motivo = checksumConfere == false
                ? "O checksum do arquivo não confere — o backup pode estar corrompido ou adulterado."
                : string.Empty
        };
    }

    private void RemoverArquivosAuxiliaresDoBanco()
    {
        foreach (var sufixo in new[] { "-wal", "-shm", "-journal" })
        {
            var caminho = _paths.DatabasePath + sufixo;
            try
            {
                if (File.Exists(caminho))
                {
                    File.Delete(caminho);
                }
            }
            catch (Exception ex)
            {
                _logger?.Warning(ex, "Falha em {Operacao}", "RemoverArquivoAuxiliarDoBanco");
            }
        }
    }

    private enum EstadoDoBancoAtivo
    {
        Saudavel,
        Corrompido,
        Indeterminado
    }

    /// <summary>
    /// Verifica o banco ativo sem escrever nada. Só é "Corrompido" quando o PRAGMA devolve algo
    /// diferente de "ok" ou o SQLite reporta CORRUPT/NOTADB; qualquer outra falha (banco ocupado,
    /// travado, I/O) é "Indeterminado" e nunca deve ser tratada como corrupção.
    /// </summary>
    private EstadoDoBancoAtivo ClassificarBancoAtivo()
    {
        try
        {
            using var conexao = new SqliteConnection($"Data Source={_paths.DatabasePath};Pooling=False;Default Timeout=2");
            conexao.Open();
            using var comando = conexao.CreateCommand();
            comando.CommandText = "PRAGMA integrity_check";
            return (string?)comando.ExecuteScalar() == "ok" ? EstadoDoBancoAtivo.Saudavel : EstadoDoBancoAtivo.Corrompido;
        }
        catch (Exception ex) when (CorrupcaoDeBanco.EhErroDeCorrupcao(ex))
        {
            return EstadoDoBancoAtivo.Corrompido;
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Falha em {Operacao}", "VerificarBancoAtivo");
            return EstadoDoBancoAtivo.Indeterminado;
        }
    }

    private Resultado<BackupRegistro> GuardarCopiaBrutaDoBancoCorrompido()
    {
        const string mensagemFalha = "Não foi possível guardar uma cópia do banco atual; restauração cancelada.";
        var agora = _relogio.UtcNow;
        try
        {
            // solta os handles em pool para o arquivo poder ser lido/copiado por inteiro
            SqliteConnection.ClearAllPools();
            Directory.CreateDirectory(_paths.BackupsDirectory);

            // fora da retenção: o nome não casa com varthex-comanda-*.db
            var nomeArquivo = $"corrompido-{agora:yyyy-MM-dd-HHmmss}.db.bak";
            var destino = Path.Combine(_paths.BackupsDirectory, nomeArquivo);
            var sufixo = 1;
            while (File.Exists(destino))
            {
                nomeArquivo = $"corrompido-{agora:yyyy-MM-dd-HHmmss}-{sufixo}.db.bak";
                destino = Path.Combine(_paths.BackupsDirectory, nomeArquivo);
                sufixo++;
            }

            File.Copy(_paths.DatabasePath, destino, overwrite: false);
            _logger?.Warning("Banco ativo corrompido; cópia bruta guardada antes da restauração");

            // StatusBackup só tem Sucesso/Falha; a cópia bruta foi guardada com sucesso.
            // Não é registrada no repositório: ele vive no próprio banco corrompido.
            return Resultado<BackupRegistro>.Ok(new BackupRegistro
            {
                Id = 0,
                Arquivo = nomeArquivo,
                Destino = _paths.BackupsDirectory,
                CriadoEm = agora,
                Status = StatusBackup.Sucesso,
                Checksum = CalcularChecksumSha256(destino),
                Mensagem = "Cópia bruta do banco corrompido, guardada antes da restauração."
            });
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "GuardarCopiaBrutaDoBancoCorrompido");
            return Resultado<BackupRegistro>.Falha(mensagemFalha);
        }
    }

    public Resultado<BackupRegistro> RestaurarPara(string caminhoArquivo)
    {
        if (!File.Exists(caminhoArquivo))
        {
            return Resultado<BackupRegistro>.Falha("Arquivo de backup não encontrado.");
        }

        string? zipDeFotos = null;
        try
        {
            // o zip de fotos e escolhido e copiado antes de a copia preventiva mexer na pasta de backups
            zipDeFotos = PrepararZipDeFotosParaRestauracao(caminhoArquivo);

            Resultado<BackupRegistro> preventivo;
            switch (ClassificarBancoAtivo())
            {
                case EstadoDoBancoAtivo.Corrompido:
                    // Banco corrompido: a cópia bruta vem ANTES de qualquer outra coisa. Nada é
                    // escrito no arquivo corrompido (CriarBackupGerenciado tenta registrar a falha
                    // no próprio banco) e o SHA-256 da cópia bate com o do arquivo original.
                    preventivo = GuardarCopiaBrutaDoBancoCorrompido();
                    if (!preventivo.Sucesso)
                    {
                        return preventivo;
                    }

                    break;

                case EstadoDoBancoAtivo.Indeterminado:
                    // Não foi possível verificar (ex.: banco ocupado/travado): não é corrupção.
                    // Cancela sem alterar nada.
                    return Resultado<BackupRegistro>.Falha("Não foi possível verificar o banco atual; restauração cancelada.");

                default:
                    preventivo = CriarBackupGerenciado();
                    if (!preventivo.Sucesso)
                    {
                        return Resultado<BackupRegistro>.Falha("Não foi possível criar a cópia preventiva; restauração cancelada.");
                    }

                    break;
            }

            var temporario = _paths.DatabasePath + ".restaurando";
            using (var origem = new SqliteConnection($"Data Source={caminhoArquivo};Pooling=False"))
            using (var destino = new SqliteConnection($"Data Source={temporario};Pooling=False"))
            {
                origem.Open();
                destino.Open();
                origem.BackupDatabase(destino);
            }

            if (!VerificarIntegridade(temporario))
            {
                File.Delete(temporario);
                return Resultado<BackupRegistro>.Falha("A restauração falhou na verificação de integridade; a base ativa não foi alterada.");
            }

            SqliteConnection.ClearAllPools();
            // arquivos auxiliares do banco antigo (WAL/SHM/journal) nao podem ser aplicados ao restaurado
            RemoverArquivosAuxiliaresDoBanco();
            File.Copy(temporario, _paths.DatabasePath, overwrite: true);
            RemoverArquivosAuxiliaresDoBanco();
            File.Delete(temporario);

            // fotos so voltam depois de o banco ter sido trocado; falha aqui nao desfaz nem falha a restauracao
            RestaurarFotosDoBackup(zipDeFotos);

            return preventivo;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "RestaurarBackup");
            return Resultado<BackupRegistro>.Falha(ex.Message);
        }
        finally
        {
            if (zipDeFotos is not null)
            {
                try
                {
                    File.Delete(zipDeFotos);
                }
                catch (Exception exLimpeza)
                {
                    _logger?.Warning(exLimpeza, "Falha em {Operacao}", "LimparZipTemporarioDeFotos");
                }
            }
        }
    }
}
