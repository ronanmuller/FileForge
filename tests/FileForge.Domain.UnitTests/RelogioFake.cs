using FileForge.SharedKernel.Abstractions;

namespace FileForge.Domain.UnitTests;

/// <summary>Relógio controlável para testes. "Hoje" é calculado no fuso do negócio, como em produção.</summary>
internal sealed class RelogioFake(DateTime utcNow) : IDateTimeProvider
{
    public RelogioFake() : this(new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc)) { }

    public DateTime UtcNow { get; set; } = utcNow;

    public DateOnly Hoje => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, SystemDateTimeProvider.SaoPaulo));
}
