using Microsoft.Data.Sqlite;

namespace VarthexComanda.Infrastructure.Persistence;

public static class CorrupcaoDeBanco
{
    private const int SqliteCorrupt = 11;
    private const int SqliteNotADb = 26;

    /// <summary>
    /// Indica se a excecao (ou alguma interna) e um erro do SQLite de arquivo corrompido
    /// (SQLITE_CORRUPT) ou que nem e um banco SQLite (SQLITE_NOTADB).
    /// </summary>
    public static bool EhErroDeCorrupcao(Exception? excecao)
    {
        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (atual is SqliteException sqlite
                && (sqlite.SqliteErrorCode == SqliteCorrupt || sqlite.SqliteErrorCode == SqliteNotADb))
            {
                return true;
            }
        }

        return false;
    }
}
