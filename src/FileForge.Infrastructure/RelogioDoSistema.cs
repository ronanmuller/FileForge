using FileForge.SharedKernel.Abstractions;

namespace FileForge.Infrastructure;

/// <summary>Relógio de produção. Os testes trocam por um valor fixo, para não depender do tempo real.</summary>
internal sealed class RelogioDoSistema : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Hoje => DateOnly.FromDateTime(UtcNow);
}
