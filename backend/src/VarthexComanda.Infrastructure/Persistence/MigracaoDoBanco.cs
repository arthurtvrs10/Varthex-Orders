using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace VarthexComanda.Infrastructure.Persistence;

public static class MigracaoDoBanco
{
    /// <summary>
    /// Backup preventivo so faz sentido quando ha migracoes pendentes E o banco ja tem migracoes
    /// aplicadas (dados existentes). Instalacao nova (nenhuma aplicada) nao tem o que preservar.
    /// </summary>
    public static bool ExigeBackupPreventivo(DatabaseFacade banco, out int pendentes)
    {
        pendentes = banco.GetPendingMigrations().Count();
        return pendentes > 0 && banco.GetAppliedMigrations().Any();
    }
}
