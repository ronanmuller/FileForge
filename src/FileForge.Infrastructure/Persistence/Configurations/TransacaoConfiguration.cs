using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileForge.Infrastructure.Persistence.Configurations;

internal sealed class TransacaoConfiguration : IEntityTypeConfiguration<Transacao>
{
    public void Configure(EntityTypeBuilder<Transacao> builder)
    {
        builder.ToTable("transacoes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(v => v.Value, v => new TransacaoId(v))
            .ValueGeneratedNever();

        builder.Property(x => x.ContaId)
            .HasConversion(v => v.Value, v => new ContaId(v))
            .IsRequired();

        builder.Property(x => x.Data)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.Valor)
            .HasConversion(v => v.Valor, v => Dinheiro.Criar(v).Value)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Tipo)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Descricao)
            .HasMaxLength(250)
            .IsRequired();

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => new { x.ContaId, x.Data });
    }
}
