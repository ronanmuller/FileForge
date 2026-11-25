using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileForge.Infrastructure.Persistence.Configurations;

internal sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("clientes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(v => v.Value, v => new ClienteId(v))
            .ValueGeneratedNever();

        builder.Property(x => x.Nome)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Documento)
            .HasConversion(v => v.Numero, v => Documento.Criar(v).Value)
            .HasMaxLength(14)
            .IsRequired();

        builder.HasIndex(x => x.Documento).IsUnique().HasDatabaseName("ux_clientes_documento");

        builder.Ignore(x => x.DomainEvents);

        builder.OwnsMany(x => x.Contas, conta =>
        {
            conta.ToTable("contas");
            conta.WithOwner().HasForeignKey("cliente_id");
            conta.Property<ClienteId>("cliente_id")
                .HasConversion(v => v.Value, v => new ClienteId(v));

            conta.HasKey(x => x.Id);
            conta.Property(x => x.Id)
                .HasColumnName("id")
                .HasConversion(v => v.Value, v => new ContaId(v))
                .ValueGeneratedNever();

            conta.Property(x => x.Agencia)
                .HasColumnName("agencia")
                .HasMaxLength(5)
                .IsRequired();

            conta.Property(x => x.Numero)
                .HasColumnName("numero")
                .HasMaxLength(12)
                .IsRequired();

            conta.HasIndex(nameof(Conta.Agencia), nameof(Conta.Numero))
                .IsUnique()
                .HasDatabaseName("ux_contas_agencia_numero");
        });

        builder.Navigation(x => x.Contas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
