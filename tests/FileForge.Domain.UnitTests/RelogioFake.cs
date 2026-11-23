using FileForge.SharedKernel.Abstractions;

namespace FileForge.Domain.UnitTests;

internal sealed class RelogioFake : IDateTimeProvider
{
    public DateTime UtcNow { get; set; } = new(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
}
