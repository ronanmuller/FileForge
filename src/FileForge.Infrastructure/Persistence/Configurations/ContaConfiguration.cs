using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileForge.Infrastructure.Persistence.Configurations;

internal sealed class ContaConfiguration : IEntityTypeConfiguration<Conta>
{
    public void Configure(EntityTypeBuilder<Conta> builder)
    {
        builder.ToTable("contas");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasConversion(v => v.Value, v => new ContaId(v))
            .ValueGeneratedNever();

        builder.Property<ClienteId>("cliente_id")
            .HasConversion(v => v.Value, v => new ClienteId(v));

        builder.Property(x => x.Agencia)
            .HasColumnName("agencia")
            .HasMaxLength(5)
            .IsRequired();

        builder.Property(x => x.Numero)
            .HasColumnName("numero")
            .HasMaxLength(12)
            .IsRequired();

        builder.HasIndex(x => new { x.Agencia, x.Numero })
            .IsUnique()
            .HasDatabaseName("ux_contas_agencia_numero");
    }
}
