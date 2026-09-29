using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class ComandaConfiguration : IEntityTypeConfiguration<Comanda>
{
    public void Configure(EntityTypeBuilder<Comanda> builder)
    {
        builder.ToTable("comanda", t =>
        {
            t.HasCheckConstraint("CK_comanda_numero_positivo", "numero > 0");
            t.HasCheckConstraint("CK_comanda_total_centavos_nao_negativo", "total_centavos >= 0");
            t.HasCheckConstraint(
                "CK_comanda_status_fechada_em",
                "(status = 'ABERTA' AND fechada_em IS NULL) OR (status IN ('FECHADA', 'CANCELADA') AND fechada_em IS NOT NULL)");
            t.HasCheckConstraint("CK_comanda_status_valido", "status IN ('ABERTA', 'FECHADA', 'CANCELADA')");
        });

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Numero).HasColumnName("numero").IsRequired();
        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<StatusComanda>(v, ignoreCase: true))
            .IsRequired();
        builder.Property(c => c.AbertaEm).HasColumnName("aberta_em").IsRequired();
        builder.Property(c => c.FechadaEm).HasColumnName("fechada_em");
        builder.Property(c => c.TotalCentavos).HasColumnName("total_centavos").HasDefaultValue(0L).IsRequired();
        builder.Property(c => c.Observacao).HasColumnName("observacao");
        builder.Property(c => c.NomeCliente).HasColumnName("nome_cliente").HasMaxLength(80);

        builder.HasIndex(c => c.Numero)
            .IsUnique()
            .HasDatabaseName("uq_comanda_numero_aberta")
            .HasFilter("status = 'ABERTA'");
    }
}
