using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileForge.Infrastructure.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Tipo)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Conteudo)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.CriadaEmUtc).IsRequired();
        builder.Property(x => x.Tentativas).IsRequired();

        builder.HasIndex(x => x.CriadaEmUtc)
            .HasDatabaseName("ix_outbox_pendentes")
            .HasFilter("publicada_em_utc IS NULL");
    }
}
