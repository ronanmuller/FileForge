using FileForge.Domain.Models;
using FileForge.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileForge.Infrastructure.Persistence.Configurations;

internal sealed class SolicitacaoArquivoConfiguration : IEntityTypeConfiguration<SolicitacaoArquivo>
{
    public void Configure(EntityTypeBuilder<SolicitacaoArquivo> builder)
    {
        builder.ToTable("solicitacoes_arquivo");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasConversion(v => v.Value, v => new SolicitacaoArquivoId(v))
            .ValueGeneratedNever();

        builder.Property(x => x.ClienteId)
            .HasConversion(v => v.Value, v => new ClienteId(v))
            .IsRequired();

        builder.Property(x => x.DataReferencia)
            .HasConversion(v => v.Valor, v => DataReferencia.Criar(v, RelogioDePersistencia.Instance).Value)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.SolicitadoPor)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.NomeArquivo)
            .HasMaxLength(200);

        builder.Property(x => x.MotivoFalha)
            .HasMaxLength(500);

        builder.Ignore(x => x.DomainEvents);

        builder.HasIndex(x => new { x.ClienteId, x.DataReferencia })
            .HasDatabaseName("ux_solicitacao_ativa")
            .IsUnique()
            .HasFilter(FiltroAtivas());
    }

    private static string FiltroAtivas() =>
        $"status IN ({string.Join(", ", SolicitacaoArquivo.StatusAtivos.Select(s => $"'{s}'"))})";

    /// <summary>
    /// Usado apenas na hidrataÃ§Ã£o do EF para reconstruir <see cref="DataReferencia"/> sem depender do relÃ³gio da aplicaÃ§Ã£o.
    /// Como o valor jÃ¡ veio persistido, qualquer data &lt;= hoje UTC Ã© vÃ¡lida.
    /// </summary>
    private sealed class RelogioDePersistencia : FileForge.SharedKernel.Abstractions.IDateTimeProvider
    {
        public static readonly RelogioDePersistencia Instance = new();

        public DateTime UtcNow => DateTime.UtcNow;

        public DateOnly Hoje => DateOnly.FromDateTime(UtcNow);
    }
}
