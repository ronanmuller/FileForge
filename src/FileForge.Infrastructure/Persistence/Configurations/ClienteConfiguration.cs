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

        builder.HasMany(x => x.Contas)
            .WithOne()
            .HasForeignKey("cliente_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Contas)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
