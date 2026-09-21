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

    private static readonly HashSet<string> ExtensoesDeFoto =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };

    public EfBackupService(AppPaths paths, IBackupRegistroRepository registros, IClock relogio, int retencaoMaxima = 30, ILogger? logger = null, PastasMascaradas? pastasMascaradas = null)
    {
        _paths = paths;
        _registros = registros;
        _relogio = relogio;
        _retencaoMaxima = retencaoMaxima;
        _logger = logger;
        _pastasMascaradas = pastasMascaradas;
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
            ZiparFotosDoBackup(destinoFinal + SufixoZipDeFotos);

            if (aplicarRetencao)
            {
                AplicarRetencao(pastaDestino);
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
            File.Delete(arquivo);
            foreach (var sufixoCompanheiro in new[] { ".sha256", SufixoZipDeFotos })
            {
                var companheiro = arquivo + sufixoCompanheiro;
                if (File.Exists(companheiro))
                {
                    File.Delete(companheiro);
                }
            }
        }
    }

    private static bool EhArquivoDeFoto(string nome) =>
        ExtensoesDeFoto.Contains(Path.GetExtension(nome));

    /// <summary>
    /// Cria o zip de fotos ao lado do backup (.db.fotos.zip) a partir de <c>fotos\</c>. Só arquivos de imagem
    /// da raiz da pasta entram, com o nome puro. O zip nasce como .tmp e só recebe o nome final quando
    /// completo. Nunca lança: falhas viram Warning e o backup do banco segue válido.
    /// </summary>
    private void ZiparFotosDoBackup(string caminhoZip)
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
                .ToList();
            if (fotos.Count == 0)
            {
                return;
            }

            using (var fluxo = new FileStream(temporario, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var zip = new ZipArchive(fluxo, ZipArchiveMode.Create))
            {
                foreach (var foto in fotos)
                {
                    zip.CreateEntryFromFile(foto, Path.GetFileName(foto), CompressionLevel.Fastest);
                }
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

    /// <summary>
    /// Extrai as fotos do zip para <c>fotos\</c> sem apagar nada: mesmo nome sobrescreve, o resto permanece.
    /// Só o nome puro da entrada é usado (nada de caminhos), só extensões de imagem, entradas com tamanho
    /// declarado acima de 10 MB são ignoradas e no máximo 5000 entradas são lidas. Nunca lança.
    /// </summary>
    private void RestaurarFotosDoBackup(string caminhoZip)
    {
        try
        {
            if (!File.Exists(caminhoZip))
            {
                return;
            }

            Directory.CreateDirectory(_paths.FotosDirectory);
            using var zip = ZipFile.OpenRead(caminhoZip);
            foreach (var entrada in zip.Entries.Take(MaximoEntradasNoZipDeFotos))
            {
                try
                {
                    ExtrairFoto(entrada);
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

    private void ExtrairFoto(ZipArchiveEntry entrada)
    {
        // Path.GetFileName descarta qualquer diretorio ("..\..\x.png" vira "x.png"): sem zip-slip
        var nome = Path.GetFileName(entrada.FullName);
        if (string.IsNullOrEmpty(nome)
            || nome.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !EhArquivoDeFoto(nome)
            || entrada.Length > TamanhoMaximoFotoBytes)
        {
            return;
        }

        var destino = Path.Combine(_paths.FotosDirectory, nome);
        var temporario = destino + ".restaurando";
        try
        {
            using (var origem = entrada.Open())
            using (var saida = new FileStream(temporario, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                // o tamanho declarado pode mentir: limita o que realmente e lido
                var buffer = new byte[81920];
                long total = 0;
                int lidos;
                while ((lidos = origem.Read(buffer, 0, buffer.Length)) > 0)
                {
                    total += lidos;
                    if (total > TamanhoMaximoFotoBytes)
                    {
                        throw new InvalidDataException("Foto acima do tamanho máximo.");
                    }

                    saida.Write(buffer, 0, lidos);
                }
            }

            File.Move(temporario, destino, overwrite: true);
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

        try
        {
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
            RestaurarFotosDoBackup(caminhoArquivo + SufixoZipDeFotos);

            return preventivo;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "RestaurarBackup");
            return Resultado<BackupRegistro>.Falha(ex.Message);
        }
    }
}
