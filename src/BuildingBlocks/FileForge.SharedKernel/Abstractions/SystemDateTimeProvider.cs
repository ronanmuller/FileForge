namespace FileForge.SharedKernel.Abstractions;

/// <summary>Relógio real do sistema. O fuso do negócio é configurável (padrão: America/Sao_Paulo).</summary>
public sealed class SystemDateTimeProvider(TimeZoneInfo fusoDoNegocio) : IDateTimeProvider
{
    public static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public SystemDateTimeProvider() : this(SaoPaulo) { }

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly Hoje => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, fusoDoNegocio));
}
